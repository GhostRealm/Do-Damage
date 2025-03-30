using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shared.resources;

namespace WorldServer.core.objects
{
    partial class Player
    {
        private int LastShootTime;
        private Dictionary<int, int> ProjectileCount = new Dictionary<int, int>();
        public bool IsValidShootTime(int time, double rateOfFire)
        {
            if (LastShootTime == time)
                return true;
            var attackPeriod = (int)(1 / Stats.GetAttackFrequency() * 1 / rateOfFire);
            if (time < LastShootTime + attackPeriod)
                return false;
            LastShootTime = time;
            return true;
        }

        public bool IsValidNumShots(int time, Item item)
        {
            foreach (var key in ProjectileCount.Keys.Where(k => k < time - 1000).ToList())
                ProjectileCount.Remove(key);

            if (!ProjectileCount.ContainsKey(time))
                ProjectileCount[time] = 0;

            return ++ProjectileCount[time] <= item.NumProjectiles;
        }

    }
}