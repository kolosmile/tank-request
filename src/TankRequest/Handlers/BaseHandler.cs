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
        protected readonly Messages _msg;

        protected BaseHandler(
            object cph,
            Dictionary<string, object> args,
            StateService stateService,
            TokenService tokenService,
            QueueService queueService,
            OverlayService overlayService,
            Config config,
            Messages messages)
        {
            _cph = cph;
            _args = args;
            _stateService = stateService;
            _tokenService = tokenService;
            _queueService = queueService;
            _overlayService = overlayService;
            _config = config;
            _msg = messages;
        }

        #region Helper Methods

        protected void SendMessage(string msg) => _cph.SendMessage(msg, true);
        protected void LogInfo(string msg) => _cph.LogInfo(msg);
        protected void LogWarn(string msg) => _cph.LogWarn(msg);
        
        /// <summary>
        /// Format a message template with named placeholders.
        /// </summary>
        protected string Msg(string template, params (string key, object value)[] values)
            => Messages.Format(template, values);
        
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
