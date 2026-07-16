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
            Config config,
            Messages messages)
            : base(cph, args, stateService, tokenService, queueService, overlayService, config, messages)
        {
        }

        /// <summary>
        /// Persist queue state and refresh the local overlay before any
        /// potentially blocking Twitch or chat operation is attempted.
        /// </summary>
        private void SaveAndRender(LedgerState state, string operation)
        {
            _stateService.Save(state);
            LogInfo($"[Overlay] State saved after {operation}; rendering {_config.QueueHtmlPath}");

            try
            {
                _overlayService.RenderQueue(state);
                LogInfo($"[Overlay] Render completed after {operation}");
            }
            catch (Exception ex)
            {
                // Overlay I/O must not prevent redemption handling or chat output.
                LogWarn($"[Overlay] Render failed after {operation}: {ex.Message}");
            }
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
                SendMessage(Msg(_msg.Error, ("user", UserName), ("error", error)));
                return;
            }

            var state = _stateService.Load();
            
            // Auto-merge logic for redemption (Manual -> Real ID)
            if (!string.IsNullOrEmpty(UserId))
            {
                MergeUserProfiles(state, "manual_" + UserName.ToLower(), UserId, UserName);
                MergeUserProfiles(state, "se_" + UserName.ToLower(), UserId, UserName);
            }

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
                string errMsg = Msg(_msg.NotEnoughTokens, ("balance", balance), ("cost", cost));
                _cph.SetArgument("displayMsg", errMsg);
                if (!string.IsNullOrEmpty(rewardId) && !string.IsNullOrEmpty(redemptionId))
                    try { _cph.TwitchRedemptionCancel(rewardId, redemptionId); } catch { }
                SendMessage(Msg(_msg.Error, ("user", UserName), ("error", errMsg)));
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
            SaveAndRender(state, "supporter redeem");

            int balanceAfter = _tokenService.GetActiveBalance(user);
            _cph.SetArgument("allow", "true");
            
            string msg;
            if (type == "Arty") 
                msg = Msg(_msg.ArtyAdded, ("tank", tank), ("user", UserName), ("balance", balanceAfter));
            else if (type == "Blacklist") 
                msg = Msg(_msg.BlacklistAdded, ("tank", tank), ("user", UserName), ("balance", balanceAfter));
            else if (type == "Troll") 
                msg = Msg(_msg.TrollAdded, ("tank", tank), ("user", UserName), ("balance", balanceAfter));
            else 
                msg = Msg(_msg.SupporterAdded, ("tank", tank), ("cost", cost), ("user", UserName), ("balance", balanceAfter));
            
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
                SendMessage(Msg(_msg.Error, ("user", UserName), ("error", error)));
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
            SaveAndRender(state, "normal redeem");

            _cph.SetArgument("allow", "true");
            string msg = Msg(_msg.NormalAdded, ("tank", tank), ("user", UserName));
            _cph.SetArgument("displayMsg", msg);
            SendMessage(msg);
            LogInfo($"[NormalRedeem] {UserName} added tank {tank} to normal queue");
        }

        public void HandleDequeue()
        {
            var state = _stateService.Load();
            var (item, isSupporter) = _queueService.DequeueTop(state);

            if (item == null)
            {
                SendMessage(_msg.QueueEmpty);
                return;
            }

            SaveAndRender(state, "dequeue");

            if (!isSupporter && !string.IsNullOrEmpty(item.rewardId) && !string.IsNullOrEmpty(item.redemptionId))
            {
                LogInfo($"[Dequeue] Fulfilling redemption {item.redemptionId}");
                try
                {
                    _cph.TwitchRedemptionFulfill(item.rewardId, item.redemptionId);
                    LogInfo($"[Dequeue] Fulfilled redemption {item.redemptionId}");
                }
                catch (Exception ex) { LogWarn($"Fulfill failed: {ex.Message}"); }
            }

            var text = item.mult > 1 ? $"{item.tank} x{item.mult}" : item.tank;
            if (item.specialType == "Arty") text = $"{item.tank} [ARTY]";
            if (item.specialType == "Blacklist") text = $"{item.tank} [BLACKLIST]";
            if (item.specialType == "Troll") text = $"{item.tank} [TROLL]";
            
            LogInfo("[Dequeue] Sending completion message");
            SendMessage(Msg(_msg.Completed, ("type", isSupporter ? "[S]" : "[N]"), ("tank", text), ("user", item.user)));
            LogInfo("[Dequeue] Completion message sent");
        }

        public void HandleRefundTop()
        {
            var state = _stateService.Load();
            var item = _queueService.RefundTopNormal(state);
            if (item == null) return;

            SaveAndRender(state, "refund top");

            if (!string.IsNullOrEmpty(item.rewardId) && !string.IsNullOrEmpty(item.redemptionId))
            {
                LogInfo($"[RefundTop] Cancelling redemption {item.redemptionId}");
                try
                {
                    _cph.TwitchRedemptionCancel(item.rewardId, item.redemptionId);
                    LogInfo($"[RefundTop] Cancelled redemption {item.redemptionId}");
                }
                catch (Exception ex) { LogWarn($"[RefundTop] Cancel failed: {ex.Message}"); }
            }

            SendMessage(Msg(_msg.RefundedNormal, ("tank", item.tank), ("user", item.user)));
        }

        public void HandleRefundAllNormal()
        {
            var state = _stateService.Load();
            var items = _queueService.RefundAllNormal(state);
            if (items.Length == 0)
            {
                SendMessage(_msg.NoNormalRequests);
                return;
            }

            SaveAndRender(state, "refund all normal");

            foreach (var item in items)
            {
                if (!string.IsNullOrEmpty(item.rewardId) && !string.IsNullOrEmpty(item.redemptionId))
                {
                    LogInfo($"[RefundAll] Cancelling redemption {item.redemptionId}");
                    try
                    {
                        _cph.TwitchRedemptionCancel(item.rewardId, item.redemptionId);
                        LogInfo($"[RefundAll] Cancelled redemption {item.redemptionId}");
                    }
                    catch (Exception ex) { LogWarn($"[RefundAll] Cancel failed: {ex.Message}"); }
                }
            }

            SendMessage(Msg(_msg.RefundedAllNormal, ("count", items.Length)));
        }

        public void HandleRenderQueue()
        {
            var state = _stateService.Load();
            try
            {
                _overlayService.RenderQueue(state);
                LogInfo($"[Overlay] Manual/startup render completed: {_config.QueueHtmlPath}");
            }
            catch (Exception ex)
            {
                LogWarn($"[Overlay] Manual/startup render failed: {ex.Message}");
            }
        }

        public void HandleQueueNormal()
        {
            if (!IsModOrBroadcaster)
            {
                SendMessage(Msg(_msg.ModOnly, ("user", UserName)));
                return;
            }
            
            if (string.IsNullOrEmpty(RawInput))
            {
                SendMessage(Msg(_msg.UsageQueueNormal, ("user", UserName)));
                return;
            }
            
            var (tank, cost, type, error) = _queueService.ParseInput(RawInput, forceMult1: true);
            if (error != null)
            {
                SendMessage(Msg(_msg.Error, ("user", UserName), ("error", error)));
                return;
            }
            
            var state = _stateService.Load();
            _queueService.AddToNormalQueue(state, new QueueItem
            {
                user = UserName, tank = tank, mult = 1,
                tsUtc = DateTime.UtcNow, raw = RawInput,
                specialType = "Normal"
            });
            SaveAndRender(state, "manual normal enqueue");
            
            SendMessage(Msg(_msg.ManualNormalAdded, ("tank", tank), ("user", UserName)));
        }

        public void HandleQueueSupporter()
        {
            if (!IsModOrBroadcaster)
            {
                SendMessage(Msg(_msg.ModOnly, ("user", UserName)));
                return;
            }
            
            if (string.IsNullOrEmpty(RawInput))
            {
                SendMessage(Msg(_msg.UsageQueueSupporter, ("user", UserName)));
                return;
            }
            
            // Check for @user target
            string targetUser = null;
            string targetUserId = null;
            string inputToParse = RawInput;
            
            if (RawInput.StartsWith("@"))
            {
                var parts = RawInput.Split(new[] { ' ' }, 2);
                if (parts.Length >= 2)
                {
                    targetUser = parts[0].TrimStart('@');
                    inputToParse = parts[1];
                    // Try to find userId from targetUser0 argument (if available from Streamer.bot)
                    targetUserId = Arg("targetUserId");
                    if (string.IsNullOrEmpty(targetUserId))
                    {
                        // Fallback: use targetUser as both name and ID (will need user lookup)
                        targetUserId = targetUser.ToLower();
                    }
                }
                else
                {
                    SendMessage(Msg(_msg.UsageQueueSupporter, ("user", UserName)));
                    return;
                }
            }
            
            var (tank, cost, type, error) = _queueService.ParseInput(inputToParse, forceMult1: false);
            if (error != null)
            {
                SendMessage(Msg(_msg.Error, ("user", UserName), ("error", error)));
                return;
            }
            
            var state = _stateService.Load();
            string queueUserName = targetUser ?? UserName;
            string queueUserId = targetUserId ?? UserId;
            
            // If @user specified, deduct tokens from that user
            if (targetUser != null)
            {
                // Find user by userName (case-insensitive)
                string foundUserId = null;
                UserState targetUserState = null;
                foreach (var kvp in state.users)
                {
                    if (kvp.Value.userName != null && 
                        kvp.Value.userName.Equals(targetUser, StringComparison.OrdinalIgnoreCase))
                    {
                        foundUserId = kvp.Key;
                        targetUserState = kvp.Value;
                        break;
                    }
                }
                
                if (targetUserState == null)
                {
                    SendMessage(Msg(_msg.UserNotFound, ("user", UserName), ("target", targetUser)));
                    return;
                }
                
                queueUserId = foundUserId;
                queueUserName = targetUserState.userName;
                
                int balance = _tokenService.GetActiveBalance(targetUserState);
                if (balance < cost)
                {
                    SendMessage(Msg(_msg.TargetNotEnoughTokens, ("target", targetUser), ("balance", balance), ("cost", cost)));
                    return;
                }
                
                _tokenService.Consume(targetUserState, cost);
            }
            
            _queueService.AddToSupporterQueue(state, new QueueItem
            {
                user = queueUserName, tank = tank, mult = cost,
                tsUtc = DateTime.UtcNow, raw = RawInput,
                specialType = type
            });
            SaveAndRender(state, "manual supporter enqueue");
            
            string msg;
            if (targetUser != null)
            {
                int balanceAfter = _tokenService.GetActiveBalance(state.users[queueUserId]);
                msg = $"Felvéve: [S] {tank} x{cost} – {targetUser}. Levonva: {cost}. Maradt: {balanceAfter}.";
            }
            else
            {
                msg = $"[MANUAL] Felvéve: [S] {tank} x{cost} – {UserName}";
            }
            if (type != "Normal" && type != "Supporter") msg += $" ({type})";
            SendMessage(msg);
        }
    }
}
