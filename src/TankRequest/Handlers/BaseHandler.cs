namespace TankRequest.Handlers
{
    using System;
    using System.Collections.Generic;
    using TankRequest.Models;
    using TankRequest.Services;

    /// <summary>
    /// Base class for all handlers providing common functionality.
    /// </summary>
    public abstract class BaseHandler
    {
        protected readonly dynamic _cph;
        protected readonly Dictionary<string, object> _args;
        protected readonly StateService _stateService;
        protected readonly TokenService _tokenService;
        protected readonly QueueService _queueService;
        protected readonly OverlayService _overlayService;
        protected readonly Config _config;

        protected BaseHandler(
            object cph,
            Dictionary<string, object> args,
            StateService stateService,
            TokenService tokenService,
            QueueService queueService,
            OverlayService overlayService,
            Config config)
        {
            _cph = cph;
            _args = args;
            _stateService = stateService;
            _tokenService = tokenService;
            _queueService = queueService;
            _overlayService = overlayService;
            _config = config;
        }

        #region Helper Methods

        protected void SendMessage(string msg) => _cph.SendMessage(msg);
        protected void LogInfo(string msg) => _cph.LogInfo(msg);
        protected void LogWarn(string msg) => _cph.LogWarn(msg);
        
        protected string Arg(string key) => _args.ContainsKey(key) ? _args[key]?.ToString() ?? "" : "";
        protected int ArgInt(string key) { int.TryParse(Arg(key), out int v); return v; }
        protected decimal ArgDecimal(string key) { decimal.TryParse(Arg(key), out decimal v); return v; }

        protected bool IsMod => Arg("isModerator") == "True";
        protected bool IsBroadcaster => Arg("userType") == "broadcaster";
        protected bool IsModOrBroadcaster => IsMod || IsBroadcaster;

        protected string UserId => Arg("userId");
        protected string UserName => Arg("userName");
        protected string RawInput => Arg("rawInput").Trim();

        #endregion
    }
}
