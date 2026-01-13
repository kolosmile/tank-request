using System;
using Newtonsoft.Json;

public class CPHInline {
  public bool Execute() {
    // Ledger törlés
    CPH.SetGlobalVar("tq.state", "", true);
    CPH.SendMessage("Tank request állapot resetelve.");
    // Overlay frissítés (ha létezik)
    try { CPH.RunAction("TankRequests - Render Queue"); } catch {}
    return true;
  }
}