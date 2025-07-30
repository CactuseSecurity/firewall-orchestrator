﻿using FWO.Basics;
using FWO.Config.Api;

namespace FWO.Ui.Services
{
    public static class JwtEventService
    {
        public static event EventHandler<string>? OnPermissionChanged;

        public static event EventHandler<string>? OnJwtAboutToExpire;

        public static event EventHandler<string>? OnJwtExpired;

        private static readonly Dictionary<string, Timer> jwtAboutToExpireTimers = new();

        private static readonly Dictionary<string, Timer> jwtExpiredTimers = new();

        public static void PermissionsChanged(string userDn)
        {
            OnPermissionChanged?.Invoke(null, userDn);
        }

        public static void JwtAboutToExpire(string userDn)
        {
            OnJwtAboutToExpire?.Invoke(null, userDn);
        }

        public static void JwtExpired(string userDn)
        {
            OnJwtExpired?.Invoke(null, userDn);
        }

        public static void AddJwtTimers(string userDn, int timeUntilyExpiry, int notificationTime)
        {
            // Dispose old timer (if existing)
            RemoveJwtTimers(userDn);

            // Create new timers
            if (notificationTime > 0 && timeUntilyExpiry - notificationTime > 0)
            {
                jwtAboutToExpireTimers[userDn] = new Timer(_ => JwtAboutToExpire(userDn), null, timeUntilyExpiry - notificationTime, int.MaxValue);
            }
            if (timeUntilyExpiry > 0)
            {
                jwtExpiredTimers[userDn] = new Timer(_ => JwtExpired(userDn), null, timeUntilyExpiry, int.MaxValue);
            }
        }

        public static void RemoveJwtTimers(string userDn)
        {
            if (jwtAboutToExpireTimers.TryGetValue(userDn, out Timer? aboutToExpire))
            {
                aboutToExpire.Dispose();
                jwtAboutToExpireTimers.Remove(userDn);
            }

            if (jwtExpiredTimers.TryGetValue(userDn, out Timer? expired))
            {
                expired.Dispose();
                jwtExpiredTimers.Remove(userDn);
            }
        }
    }
}
