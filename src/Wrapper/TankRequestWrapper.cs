/*
 * TankRequest Wrapper - Minimal
 * 
 * Just creates the controller and calls Execute().
 * All logic is in TankRequest.dll.
 */

using System;
using TankRequest;

public class CPHInline
{
    public bool Execute()
    {
        var controller = new TankRequestController(CPH, args);
        return controller.Execute();
    }
}
