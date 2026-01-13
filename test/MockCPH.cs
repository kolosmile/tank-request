// Mock CPH implementáció - Streamer.bot nélküli teszteléshez
// Szimulálja a CPH API-t: globális változók, üzenetküldés, logolás

using System;
using System.Collections.Generic;

/// <summary>
/// Mock implementáció a Streamer.bot CPH API-jához.
/// Használat: CPH = new MockCPH(); majd a scriptek futtatása.
/// </summary>
public class MockCPH
{
    // Globális változók tárolása (persisted és non-persisted)
    private Dictionary<string, object> _persistedVars = new();
    private Dictionary<string, object> _nonPersistedVars = new();
    
    // Argumentumok (SetArgument/TryGetArg)
    private Dictionary<string, object> _arguments = new();
    
    // Log minden műveletről
    public List<string> Logs { get; } = new();
    public List<string> ChatMessages { get; } = new();
    public List<string> ActionsCalled { get; } = new();

    // === GLOBAL VARIABLES ===
    
    public T GetGlobalVar<T>(string name, bool persisted = true)
    {
        var dict = persisted ? _persistedVars : _nonPersistedVars;
        if (dict.TryGetValue(name, out var val))
        {
            Logs.Add($"[GET] {name} = {val}");
            if (val is T typed) return typed;
            // Próbáljuk konvertálni
            try { return (T)Convert.ChangeType(val, typeof(T)); }
            catch { return default; }
        }
        Logs.Add($"[GET] {name} = (null/default)");
        return default;
    }

    public void SetGlobalVar(string name, object value, bool persisted = true)
    {
        var dict = persisted ? _persistedVars : _nonPersistedVars;
        dict[name] = value;
        var preview = value?.ToString()?.Substring(0, Math.Min(100, value?.ToString()?.Length ?? 0));
        Logs.Add($"[SET] {name} = {preview}...");
    }

    // === MESSAGING ===
    
    public void SendMessage(string message, bool bot = false)
    {
        ChatMessages.Add(message);
        Logs.Add($"[CHAT] {message}");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"💬 CHAT: {message}");
        Console.ResetColor();
    }

    // === ARGUMENTS ===
    
    public void SetArgument(string name, object value)
    {
        _arguments[name] = value;
        Logs.Add($"[ARG] {name} = {value}");
    }

    public bool TryGetArg(string name, out object value)
    {
        return _arguments.TryGetValue(name, out value);
    }

    public T GetArg<T>(string name)
    {
        if (_arguments.TryGetValue(name, out var val))
        {
            if (val is T typed) return typed;
            try { return (T)Convert.ChangeType(val, typeof(T)); }
            catch { return default; }
        }
        return default;
    }

    // === LOGGING ===
    
    public void LogInfo(string message)
    {
        Logs.Add($"[INFO] {message}");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"ℹ️  INFO: {message}");
        Console.ResetColor();
    }

    public void LogWarn(string message)
    {
        Logs.Add($"[WARN] {message}");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"⚠️  WARN: {message}");
        Console.ResetColor();
    }

    public void LogError(string message)
    {
        Logs.Add($"[ERROR] {message}");
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"❌ ERROR: {message}");
        Console.ResetColor();
    }

    public void LogDebug(string message)
    {
        Logs.Add($"[DEBUG] {message}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"🔍 DEBUG: {message}");
        Console.ResetColor();
    }

    // === ACTION RUNNING ===
    
    public bool RunAction(string actionName, bool runImmediately = true)
    {
        ActionsCalled.Add(actionName);
        Logs.Add($"[ACTION] {actionName}");
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine($"🎬 ACTION: {actionName}");
        Console.ResetColor();
        return true;
    }

    // === HELPER METHODS for test setup ===
    
    /// <summary>Beállít egy non-persisted változót (tmp.* prefix nélkül is működik)</summary>
    public void SetupTempVar(string name, object value)
    {
        _nonPersistedVars[name] = value;
    }

    /// <summary>Beállít egy persisted változót</summary>
    public void SetupPersistedVar(string name, object value)
    {
        _persistedVars[name] = value;
    }

    /// <summary>Konzolra írja az összes logot</summary>
    public void DumpLogs()
    {
        Console.WriteLine("\n=== FULL LOG ===");
        foreach (var log in Logs)
            Console.WriteLine(log);
    }

    /// <summary>Visszaadja a tárolt state-et olvasható formában</summary>
    public string GetStateJson()
    {
        if (_persistedVars.TryGetValue("tq.state", out var state))
            return state?.ToString() ?? "{}";
        return "{}";
    }

    /// <summary>Törli az összes állapotot - új teszt előtt</summary>
    public void Reset()
    {
        _persistedVars.Clear();
        _nonPersistedVars.Clear();
        _arguments.Clear();
        Logs.Clear();
        ChatMessages.Clear();
        ActionsCalled.Clear();
    }
}
