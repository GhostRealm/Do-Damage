using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Linq;

namespace Shared.database.leaderboard
{
    /// One death on the server, as the Legends screen's Recent Deaths lists it.
    public class DbRecentDeath
    {
        public int AccountId { get; set; }
        public int CharId { get; set; }
        public string Name { get; set; }
        public ushort ObjectType { get; set; }
        public int Skin { get; set; }
        public int Tex1 { get; set; }
        public int Tex2 { get; set; }
        public int Level { get; set; }
        public int TotalFame { get; set; }
        public string Killer { get; set; }
        /// Unix time.
        public int Time { get; set; }
        /// The world the character died in.
        public string World { get; set; }
    }

    /// The server's latest deaths, newest first, in the capped list `deaths:recent` (JSON entries).
    public static class DbRecentDeaths
    {
        private const string Key = "deaths:recent";

        /// How many the list keeps; the client shows 20.
        public const int Max = 30;

        public static void Insert(IDatabase db, DbRecentDeath death)
        {
            var trans = db.CreateTransaction();
            trans.ListLeftPushAsync(Key, JsonConvert.SerializeObject(death), When.Always, CommandFlags.FireAndForget);
            trans.ListTrimAsync(Key, 0, Max - 1, CommandFlags.FireAndForget);
            trans.ExecuteAsync(CommandFlags.FireAndForget);
        }

        public static DbRecentDeath[] Get(IDatabase db, int count)
        {
            return db.ListRange(Key, 0, Math.Clamp(count, 1, Max) - 1)
                .Select(e =>
                {
                    try { return JsonConvert.DeserializeObject<DbRecentDeath>(e); }
                    catch (JsonException) { return null; }
                })
                .Where(e => e != null)
                .ToArray();
        }
    }
}
