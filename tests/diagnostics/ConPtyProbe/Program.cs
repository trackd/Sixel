using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

#pragma warning disable CA1838, SYSLIB1054 // Standalone Win32 diagnostic using mutable CreateProcess command line.

// Runs an isolated child console, not the user's terminal or PSReadLine input.
// dotnet run --project tests/diagnostics/ConPtyProbe -- <conpty.dll path>
const string rgb = "\x1b_Ga=T,f=24,s=1,v=1,q=2; /wAA\x1b\\";
const string query = "\x1b_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA\x1b\\";
const string da = "\x1b[?65;1;3;4;7;9;18;21;22;29;52;314c";
if (args.Length > 0 && args[0] == "child") {
    IntPtr childInput = Native.CreateFileW("CONIN$", 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
    IntPtr childOutput = Native.CreateFileW("CONOUT$", 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
    Native.SetStdHandle(-10, childInput);
    Native.SetStdHandle(-11, childOutput);
    if (args.Length > 2 && args[2] == "shell") {
        Native.SetHandleInformation(childInput, 1, 1);
        Native.SetHandleInformation(childOutput, 1, 1);
        var shellStartup = new Native.StartupInfoEx();
        shellStartup.Info.Size = Marshal.SizeOf<Native.StartupInfo>();
        shellStartup.Info.Flags = 0x100;
        shellStartup.Info.Input = childInput;
        shellStartup.Info.Output = shellStartup.Info.Error = childOutput;
        string shellCommand = "pwsh.exe -NoLogo -NoProfile -NoExit -File \"" + Path.GetFullPath("tests/diagnostics/ConPtyProbe/AutoImport.ps1") +
            "\" -ResultPath \"" + args[1] + "\"" + (args.Length > 3 && args[3] == "setin" ? " -ReplaceConsoleInput" : "");
        if (!Native.CreateProcessW(null, new StringBuilder(shellCommand), IntPtr.Zero, IntPtr.Zero, true, 0,
            IntPtr.Zero, null, ref shellStartup, out var shell)) throw new System.ComponentModel.Win32Exception();
        Native.CloseHandle(shell.Thread);
        if (Native.WaitForSingleObject(shell.Process, 25000) != 0) Native.TerminateProcess(shell.Process, 1);
        Native.CloseHandle(shell.Process);
        return;
    }
    if (args.Length > 2 && args[2] == "vt") {
        Native.GetConsoleMode(childInput, out uint mode);
        if (!Native.SetConsoleMode(childInput, (mode | 0x200) & ~6u)) throw new System.ComponentModel.Win32Exception();
    }
    Console.Write("BEGIN" + rgb.Replace("; ", ";") + "\x1b_Ga=T,f=100,q=2;iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aG1sAAAAASUVORK5CYII=\x1b\\" + "END");
    Console.Out.Flush();
    Thread.Sleep(100);
    Native.GetConsoleMode(childInput, out uint beforeMode);
    string? parsed = null;
    if (args.Length > 2 && args[2] == "fixed") {
        var assembly = Assembly.LoadFrom(args[3]);
        var transportType = assembly.GetType("Sixel.Terminal.ConsoleQueryTransport", throwOnError: true)!;
        var queryType = assembly.GetType("Sixel.Terminal.TerminalQuery", throwOnError: true)!;
        object transport = Activator.CreateInstance(transportType, nonPublic: true)!;
        string batch = (string)queryType.GetField("RuntimeProbe", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!;
        parsed = (string)queryType.GetMethod("Read", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [batch, transport])!;
    }
    else {
        Console.Write(query + "\x1b[c");
        Console.Out.Flush();
    }
    Native.GetConsoleMode(childInput, out uint afterMode);
    var read = new StringBuilder();
    var watch = Stopwatch.StartNew();
    while (watch.ElapsedMilliseconds < 1000) {
        if (Console.KeyAvailable) read.Append(Console.ReadKey(true).KeyChar);
        else Thread.Sleep(1);
    }
    File.WriteAllText(args[1], JsonSerializer.Serialize(new { Received = read.ToString(), Parsed = parsed, ModeRestored = beforeMode == afterMode }));
    return;
}
if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("ConPTY diagnostic is Windows-only.");
string library = args.Length > 0 ? args[0] : "kernel32.dll";
bool shellTest = args.Length > 1 && args[1] == "shell";
bool forceTest = shellTest && args.Length > 3 && args[3] == "force";
int replyDelay = shellTest && args.Length > 2 ? int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 0;
IntPtr dll = NativeLibrary.Load(library);
var create = Marshal.GetDelegateForFunctionPointer<Native.CreatePseudoConsole>(NativeLibrary.GetExport(dll, "CreatePseudoConsole"));
var close = Marshal.GetDelegateForFunctionPointer<Native.ClosePseudoConsole>(NativeLibrary.GetExport(dll, "ClosePseudoConsole"));
using var input = new AnonymousPipeServerStream(PipeDirection.Out);
using var output = new AnonymousPipeServerStream(PipeDirection.In);
int hr = create(new Native.Coord { X = 80, Y = 24 }, input.ClientSafePipeHandle.DangerousGetHandle(), output.ClientSafePipeHandle.DangerousGetHandle(), 0, out IntPtr console);
Marshal.ThrowExceptionForHR(hr);
input.DisposeLocalCopyOfClientHandle();
output.DisposeLocalCopyOfClientHandle();
IntPtr attributes = IntPtr.Zero;
string resultPath = Path.Combine(Path.GetTempPath(), "sixel-conpty-" + Guid.NewGuid().ToString("N") + ".json");
try {
    IntPtr size = IntPtr.Zero;
    Native.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
    attributes = Marshal.AllocHGlobal(size);
    if (!Native.InitializeProcThreadAttributeList(attributes, 1, 0, ref size) ||
        !Native.UpdateProcThreadAttribute(attributes, 0, (IntPtr)0x20016, console, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
        throw new System.ComponentModel.Win32Exception();
    var startup = new Native.StartupInfoEx { Attributes = attributes };
    startup.Info.Size = Marshal.SizeOf<Native.StartupInfoEx>();
    string command = "dotnet \"" + Assembly.GetExecutingAssembly().Location + "\" child \"" + resultPath + "\"" + (args.Length > 1 ? " " + args[1] : "") +
        (!shellTest && args.Length > 2 ? " \"" + Path.GetFullPath(args[2]) + "\"" : "");
    if (shellTest && args.Length > 3 && args[3] == "setin") command += " setin";
    if (!Native.CreateProcessW(null, new StringBuilder(command), IntPtr.Zero, IntPtr.Zero, false, 0x80000, IntPtr.Zero, null, ref startup, out var process))
        throw new System.ComponentModel.Win32Exception();
    Native.CloseHandle(process.Thread);
    var captured = new StringBuilder();
    int shellPhase = 0;
    int editingQueries = 0;
    int executionQueries = 0;
    Task? typing = null;
    Task? nextTyping = null;
    var replies = new List<Task>();
    object inputLock = new();
    void Send(string value) {
        lock (inputLock) { input.Write(Encoding.UTF8.GetBytes(value)); input.Flush(); }
    }
    var reader = Task.Run(() => {
        var buffer = new byte[8192];
        var pending = new StringBuilder();
        while (true) {
            int count;
            try { count = output.Read(buffer, 0, buffer.Length); }
            catch (IOException) { break; }
            if (count == 0) break;
            string text = Encoding.UTF8.GetString(buffer, 0, count);
            lock (captured) captured.Append(text);
            pending.Append(text);
            if (shellTest) {
                string accumulated = pending.ToString();
                if (shellPhase == 0 && accumulated.Contains("EDIT-BEGIN", StringComparison.Ordinal)) {
                    shellPhase = 1;
                    typing = Task.Run(async () => {
                        Send("ConvertTo-Sixel -Path .\\assets\\co\t");
                        await Task.Delay(1500);
                        Send(" -Width 2" + (forceTest ? " -Force" : "") + " | Out-Null\r");
                    });
                }
                if (shellPhase == 1 && accumulated.Contains("EXEC-BEGIN", StringComparison.Ordinal)) shellPhase = 2;
                if (shellPhase == 2 && accumulated.Contains("EDIT-AGAIN", StringComparison.Ordinal)) {
                    shellPhase = 3;
                    nextTyping = Task.Run(async () => {
                        if (replyDelay > 0) await Task.Delay(replyDelay + 300);
                        Send("typed-after-query\r");
                    });
                }
            }
            foreach (var request in new[] { ("\x1b[6n", "\x1b[1;1R"), (query, "\x1b_Gi=31;OK\x1b\\"),
                ("\x1b[>q", "\x1bP>|contour 0.7.0-test\x1b\\"), ("\x1b[16t", "\x1b[6;20;10t"),
                ("\x1b[14t", "\x1b[4;480;800t"), ("\x1b[18t", "\x1b[8;24;80t"), ("\x1b[c", da) }) {
                int at;
                while ((at = pending.ToString().IndexOf(request.Item1, StringComparison.Ordinal)) >= 0) {
                    if (shellTest && request.Item1 != "\x1b[6n") {
                        if (shellPhase == 1) editingQueries++;
                        if (shellPhase == 2 && request.Item1 == query) executionQueries++;
                    }
                    if (shellTest && shellPhase == 2 && replyDelay > 0) {
                        string replyText = request.Item2;
                        Task previous = replies.Count == 0 ? Task.Delay(replyDelay) : replies[replies.Count - 1];
                        replies.Add(Task.Run(async () => { await previous; Send(replyText); }));
                    }
                    else Send(request.Item2);
                    pending.Remove(at, request.Item1.Length);
                }
            }
        }
    });
    uint wait = Native.WaitForSingleObject(process.Process, shellTest ? 30000u : 10000u);
    if (wait != 0) Native.TerminateProcess(process.Process, 1);
    Native.CloseHandle(process.Process);
    // Closing pseudoconsole after child exit; reader has already consumed its output.
    close(console); console = IntPtr.Zero;
    await reader.WaitAsync(TimeSpan.FromSeconds(3));
    if (typing is not null) {
        try { await typing; }
        catch (IOException) { /* Child failure is reported with its captured output below. */ }
    }
    if (nextTyping is not null) await nextTyping;
    await Task.WhenAll(replies);
    string wire = captured.ToString();
    if (shellTest) {
        string child = File.Exists(resultPath) ? File.ReadAllText(resultPath) : "{}";
        string wirePath = Path.GetFullPath("tests/diagnostics/ConPtyProbe/obj/shell-last-wire.json");
        File.WriteAllText(wirePath, JsonSerializer.Serialize(wire));
        Console.WriteLine(JsonSerializer.Serialize(new { Child = child, editingQueries, executionQueries, replyDelay, WirePath = wirePath }));
        using var result = JsonDocument.Parse(child);
        if (wait != 0 || editingQueries != 0 || executionQueries != (forceTest ? 0 : 1) ||
            !result.RootElement.GetProperty("ImportedByCompletion").GetBoolean() ||
            result.RootElement.GetProperty("ProbeBeforeExecution").GetBoolean() ||
            result.RootElement.GetProperty("ProbeAfterExecution").GetBoolean() == forceTest ||
            result.RootElement.GetProperty("NextLine").GetString() != "typed-after-query")
            throw new InvalidOperationException("Fresh auto-import/command execution/editor handoff failed.");
        return;
    }
    if (args.Length > 1 && args[1] == "fixed") {
        using var result = JsonDocument.Parse(File.ReadAllText(resultPath));
        string parsed = result.RootElement.GetProperty("Parsed").GetString() ?? "";
        if (!parsed.Contains("\x1b_Gi=31;OK\x1b\\", StringComparison.Ordinal) || !parsed.EndsWith(da, StringComparison.Ordinal) ||
            result.RootElement.GetProperty("Received").GetString() != "" || !result.RootElement.GetProperty("ModeRestored").GetBoolean())
            throw new InvalidOperationException("Probe did not collect the Kitty+DA exchange cleanly or restore input mode.");
    }
    Console.WriteLine(JsonSerializer.Serialize(new {
        Library = library, ChildExited = wait == 0,
        RgbForwarded = wire.Contains(rgb.Replace("; ", ";"), StringComparison.Ordinal),
        PngForwarded = wire.Contains("\x1b_Ga=T,f=100", StringComparison.Ordinal),
        KittyQueryForwarded = wire.Contains(query, StringComparison.Ordinal),
        DaForwarded = wire.Contains("\x1b[c", StringComparison.Ordinal),
        Child = File.Exists(resultPath) ? File.ReadAllText(resultPath) : null,
        Captured = wire
    }, new JsonSerializerOptions { WriteIndented = true }));
}
finally {
    if (console != IntPtr.Zero) close(console);
    if (attributes != IntPtr.Zero) { Native.DeleteProcThreadAttributeList(attributes); Marshal.FreeHGlobal(attributes); }
    File.Delete(resultPath);
    NativeLibrary.Free(dll);
}

static class Native {
    [StructLayout(LayoutKind.Sequential)] internal struct Coord { internal short X, Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct StartupInfo {
        internal int Size; internal IntPtr Reserved, Desktop, Title;
        internal int X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
        internal short ShowWindow, Reserved2Count; internal IntPtr Reserved2, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct StartupInfoEx { internal StartupInfo Info; internal IntPtr Attributes; }
    [StructLayout(LayoutKind.Sequential)] internal struct ProcessInfo { internal IntPtr Process, Thread; internal uint ProcessId, ThreadId; }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate int CreatePseudoConsole(Coord size, IntPtr input, IntPtr output, uint flags, out IntPtr console);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate void ClosePseudoConsole(IntPtr console);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, uint flags, ref IntPtr size);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previous, IntPtr returned);
    [DllImport("kernel32.dll")] internal static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool CreateProcessW(string? application, StringBuilder command, IntPtr processAttributes, IntPtr threadAttributes, bool inherit, uint flags, IntPtr environment, string? directory, ref StartupInfoEx startup, out ProcessInfo process);
    [DllImport("kernel32.dll")] internal static extern uint WaitForSingleObject(IntPtr handle, uint timeout);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool TerminateProcess(IntPtr handle, uint code);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern IntPtr CreateFileW(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetStdHandle(int id, IntPtr handle);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool GetConsoleMode(IntPtr handle, out uint mode);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool SetConsoleMode(IntPtr handle, uint mode);
}
