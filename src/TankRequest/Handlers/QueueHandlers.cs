namespace TankRequest.Handlers
{
    using System;
    using System.Collections.Generic;
    using TankRequest.Models;
    using TankRequest.Services;

    /// <summary>
    /// Handlers for queue-related operations.
    /// </summary>
    public class QueueHandlers : BaseHandler
    {
        public QueueHandlers(
            object cph,
            Dictionary<string, object> args,
            StateService stateService,
            TokenService tokenService,
            QueueService queueService,
            OverlayService overlayService,
            Config config)
            : base(cph, args, stateService, tokenService, queueService, overlayService, config)
        {
        }

        public void HandleSupporterRedeem()
        {
            string redemptionId = Arg("redemptionId");
            string rewardId = Arg("rewardId");

            var (tank, cost, type, error) = _queueService.ParseInput(RawInput, forceMult1: false);
            if (error != null)
            {
                _cph.SetArgument("allow", "false");
                _cph.SetArgument("displayMsg", error);
                if (!string.IsNullOrEmpty(rewardId) && !string.IsNullOrEmpty(redemptionId))
                    try { _cph.TwitchRedemptionCancel(rewardId, redemptionId); } catch { }
                SendMessage($"@{UserName}, {error}");
                return;
            }

            var state = _stateService.Load();
            if (!state.users.TryGetValue(UserId, out var user))
            {
                user = new UserState();
                state.users[UserId] = user;
            }
            user.userName = UserName;

            int balance = _tokenService.GetActiveBalance(user);
            if (balance < cost)
            {
                _cph.SetArgument("allow", "false");
                string errMsg = $"Nincs elég tokened (van: {balance}, kell: {cost}).";
                _cph.SetArgument("displayMsg", errMsg);
                if (!string.IsNullOrEmpty(rewardId) && !string.IsNullOrEmpty(redemptionId))
                    try { _cph.TwitchRedemptionCancel(rewardId, redemptionId); } catch { }
                SendMessage($"@{UserName}, {errMsg}");
                return;
            }

            _tokenService.Consume(user, cost);
            _queueService.AddToSupporterQueue(state, new QueueItem
            {
                user = UserName, tank = tank, mult = cost,
                tsUtc = DateTime.UtcNow, raw = RawInput,
                redemptionId = redemptionId, rewardId = rewardId,
                specialType = type
            });
            _stateService.Save(state);
            _overlayService.RenderQueue(state);

            int balanceAfter = _tokenService.GetActiveBalance(user);
            _cph.SetArgument("allow", "true");
            
            string msg = $"Felvéve: [S] {tank} x{cost} – {UserName}. Maradt: {balanceAfter}.";
            if (type == "Arty") msg = $"Arty kérés kiváltva: {tank} (5 token). Maradt: {balanceAfter}.";
            else if (type == "Blacklist") msg = $"Feketelistás kérés kiváltva: {tank} (3 token). Maradt: {balanceAfter}.";
            else if (type == "Troll") msg = $"Troll kérés kiváltva: {tank} (10 token). Maradt: {balanceAfter}.";
            
            _cph.SetArgument("displayMsg", msg);
            SendMessage(msg);
            
            if (!string.IsNullOrEmpty(rewardId) && !string.IsNullOrEmpty(redemptionId))
                _cph.TwitchRedemptionFulfill(rewardId, redemptionId);
        }

        public void HandleNormalRedeem()
        {
            string redemptionId = Arg("redemptionId");
            string rewardId = Arg("rewardId");

            var (tank, cost, type, error) = _queueService.ParseInput(RawInput, forceMult1: true);
            if (error != null)
            {
                _cph.SetArgument("allow", "false");
                _cph.SetArgument("displayMsg", error);
                if (!string.IsNullOrEmpty(rewardId) && !string.IsNullOrEmpty(redemptionId))
                    try { _cph.TwitchRedemptionCancel(rewardId, redemptionId); } catch { }
                SendMessage($"@{UserName}, {error}");
                return;
            }

            var state = _stateService.Load();
            _queueService.AddToNormalQueue(state, new QueueItem
            {
                user = UserName, tank = tank, mult = 1,
                tsUtc = DateTime.UtcNow, raw = RawInput,
                redemptionId = redemptionId, rewardId = rewardId,
                specialType = "Normal"
            });
            _stateService.Save(state);
            _overlayService.RenderQueue(state);

            _cph.SetArgument("allow", "true");
            string msg = $"Felvéve: [N] {tank} – {UserName}";
            _cph.SetArgument("displayMsg", msg);
            SendMessage(msg);
        }

        public void HandleDequeue()
        {
            var state = _stateService.Load();
            var (item, isSupporter) = _queueService.DequeueTop(state);

            if (item == null)
            {
                SendMessage("A sor üres.");
                return;
            }

            _stateService.Save(state);

            if (!isSupporter && !string.IsNullOrEmpty(item.rewardId) && !string.IsNullOrEmpty(item.redemptionId))
            {
                try { _cph.TwitchRedemptionFulfill(item.rewardId, item.redemptionId); }
                catch (Exception ex) { LogWarn($"Fulfill failed: {ex.Message}"); }
            }

            var text = item.mult > 1 ? $"{item.tank} x{item.mult}" : item.tank;
            if (item.specialType == "Arty") text = $"{item.tank} [ARTY]";
            if (item.specialType == "Blacklist") text = $"{item.tank} [BLACKLIST]";
            if (item.specialType == "Troll") text = $"{item.tank} [TROLL]";
            
            SendMessage($"Teljesítve: {(isSupporter ? "[S]" : "[N]")} {text} — {item.user}");
            _overlayService.RenderQueue(state);
        }

        public void HandleRefundTop()
        {
            var state = _stateService.Load();
            var item = _queueService.RefundTopNormal(state);
            if (item == null) return;

            _stateService.Save(state);

            if (!string.IsNullOrEmpty(item.rewardId) && !string.IsNullOrEmpty(item.redemptionId))
            {
                try { _cph.TwitchRedemptionCancel(item.rewardId, item.redemptionId); }
                catch { }
            }

            SendMessage($"Visszavonva: [N] {item.tank} — {item.user} (pont visszaadva)");
            _overlayService.RenderQueue(state);
        }

        public void HandleRefundAllNormal()
        {
            var state = _stateService.Load();
            var items = _queueService.RefundAllNormal(state);
            if (items.Length == 0)
            {
                SendMessage("Nincs normál kérés a sorban.");
                return;
            }

            _stateService.Save(state);

            foreach (var item in items)
            {
                if (!string.IsNullOrEmpty(item.rewardId) && !string.IsNullOrEmpty(item.redemptionId))
                    try { _cph.TwitchRedemptionCancel(item.rewardId, item.redemptionId); } catch { }
            }

            SendMessage($"{items.Length} normál kérés visszavonva, pontok visszaadva.");
            _overlayService.RenderQueue(state);
        }

        public void HandleRenderQueue()
        {
            var state = _stateService.Load();
            _overlayService.RenderQueue(state);
            LogInfo($"Queue HTML frissítve: {_config.QueueHtmlPath}");
        }

        public void HandleQueueNormal()
        {
            if (!IsModOrBroadcaster)
            {
                SendMessage($"@{UserName}, csak mod/broadcaster használhatja.");
                return;
            }
            
            if (string.IsNullOrEmpty(RawInput))
            {
                SendMessage($"@{UserName}, használat: !queuenormal [tank név]");
                return;
            }
            
            var (tank, cost, type, error) = _queueService.ParseInput(RawInput, forceMult1: true);
            if (error != null)
            {
                SendMessage($"@{UserName}, {error}");
                return;
            }
            
            var state = _stateService.Load();
            _queueService.AddToNormalQueue(state, new QueueItem
            {
                user = UserName, tank = tank, mult = 1,
                tsUtc = DateTime.UtcNow, raw = RawInput,
                specialType = "Normal"
            });
            _stateService.Save(state);
            _overlayService.RenderQueue(state);
            
            SendMessage($"[TESZT] Felvéve: [N] {tank} – {UserName}");
        }

        public void HandleQueueSupporter()
        {
            if (!IsModOrBroadcaster)
            {
                SendMessage($"@{UserName}, csak mod/broadcaster használhatja.");
                return;
            }
            
            if (string.IsNullOrEmpty(RawInput))
            {
                SendMessage($"@{UserName}, használat: !queuesupporter [tank név] [szorzó]");
                return;
            }
            
            var (tank, cost, type, error) = _queueService.ParseInput(RawInput, forceMult1: false);
            if (error != null)
            {
                SendMessage($"@{UserName}, {error}");
                return;
            }
            
            var state = _stateService.Load();
            _queueService.AddToSupporterQueue(state, new QueueItem
            {
                user = UserName, tank = tank, mult = cost,
                tsUtc = DateTime.UtcNow, raw = RawInput,
                specialType = type
            });
            _stateService.Save(state);
            _overlayService.RenderQueue(state);
            
            string msg = $"[TESZT] Felvéve: [S] {tank} x{cost} – {UserName}";
            if (type != "Normal" && type != "Supporter") msg += $" ({type})";
            SendMessage(msg);
        }
    }
}
