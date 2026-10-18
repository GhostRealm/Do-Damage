using System;
using System.Collections.Generic;
using System.Linq;
using Shared;
using Shared.resources;
using WorldServer.core.objects;
using WorldServer.networking.packets.outgoing;

namespace WorldServer.core.worlds.impl
{
    // The Support Bot by the Nexus spawn: it tells new players about /gear (now and then, and to
    // each player who comes near) and every ten minutes puts out a wave of GIFT_COUNT XP gifts
    // around the spawn (an admin's "/spawn 100 xp gift"), a new wave only once every gift of the
    // last one has been shot down. Both are objects the client already knows (Support Bot from
    // Doomed Realms' pets, XP Gift from the testing objects: 600 HP, no attacks, the usual kill
    // experience); the bot's pet behaviour is cleared, so it stands still.
    public sealed class NexusGuide
    {
        private const string BOT = "Support Bot";
        private const string GIFT = "XP Gift";
        private const string NAME = "#Support Bot";
        private const int GIFT_COUNT = 100;
        private const float GIFT_INTERVAL = 600f;   // seconds
        private const float TALK_INTERVAL = 45f;
        private const float TALK_RANGE = 15f;
        private const float GREET_RANGE = 5f;
        private const float GREET_AGAIN = 300f;     // a player is greeted again after this long

        // beside the spawn rug (tools/build_castle_maps.py, CENTRE (48, 42), the rug 5 tiles
        // square, the plaza 10.5 tiles round): the bot left of the Armoire, the gifts on the open
        // plaza tiles around the rug
        private static readonly (float X, float Y) BotSpot = (46.5f, 46.5f);
        private static readonly (float X, float Y) Centre = (48.5f, 42.5f);
        private const float GIFT_MIN_RADIUS = 3.5f;
        private const float GIFT_MAX_RADIUS = 10f;

        // tips for new players, said in turn
        private static readonly string[] Lines =
        {
            "New here? Type /gear for free starter gear!",
            "Type /godlands to jump straight to the Godlands, the realm's toughest and richest part. Get some levels first!",
            "Shoot the XP gifts for experience! I put out more every ten minutes, once they're all gone.",
            "Type /realm to go to a realm, and /nexus to come back here from anywhere.",
            "Stand by the fountains around the spawn to heal.",
            "Type /vault to store items in your vault; they stay safe when a character dies.",
            "Type /shop to see the shops: abilities, weapons, armor, potions and keys for fame.",
            "Type /sellfame <slot> to sell items you don't need for fame.",
            "Type /tell <name> <message> to whisper, and /trade <name> to trade.",
            "Type /commands to see every command you can use.",
        };

        private readonly World _world;
        private Entity _bot;
        private readonly List<Entity> _gifts = new List<Entity>();
        private float _giftTimer;
        private float _talkTimer = TALK_INTERVAL;
        private float _greetCheck;
        private int _line;
        private readonly Dictionary<int, float> _greeted = new Dictionary<int, float>();   // player id, time left
        private float _clock;

        public NexusGuide(World world) => _world = world;

        public void Init()
        {
            var (x, y) = FindSpot(BotSpot);
            _bot = _world.CreateNewEntity(BOT, x, y);
            if (_bot == null)
                return;
            _bot.SwitchTo(null);    // not the pet that follows its owner
            _bot.ApplyPermanentConditionEffect(ConditionEffectIndex.Invincible);
        }

        public void Update(ref TickTime time)
        {
            if (_bot == null)
                return;
            var dt = time.DeltaTime;
            _clock += dt;

            _giftTimer -= dt;
            if (_giftTimer <= 0)
            {
                _giftTimer = GIFT_INTERVAL;
                _gifts.RemoveAll(g => g.Dead);
                if (_gifts.Count == 0)
                    SpawnGifts();
            }

            _talkTimer -= dt;
            if (_talkTimer <= 0)
            {
                _talkTimer = TALK_INTERVAL;
                SayNear(Lines[_line]);
                _line = (_line + 1) % Lines.Length;
            }

            _greetCheck -= dt;
            if (_greetCheck <= 0)
            {
                _greetCheck = 0.5f;
                Greet();
            }
        }

        // GIFT_COUNT gifts, each on its own open plaza tile (no object on it) around the rug
        private void SpawnGifts()
        {
            var tiles = new List<(int X, int Y)>();
            for (var y = (int)(Centre.Y - GIFT_MAX_RADIUS); y <= Centre.Y + GIFT_MAX_RADIUS; y++)
                for (var x = (int)(Centre.X - GIFT_MAX_RADIUS); x <= Centre.X + GIFT_MAX_RADIUS; x++)
                {
                    var d = MathF.Sqrt((x + 0.5f - Centre.X) * (x + 0.5f - Centre.X) + (y + 0.5f - Centre.Y) * (y + 0.5f - Centre.Y));
                    if (d < GIFT_MIN_RADIUS || d > GIFT_MAX_RADIUS || !_world.Map.Contains(x, y) || _world.Map[x, y].ObjType != 0)
                        continue;
                    if ((int)BotSpot.X == x && (int)BotSpot.Y == y)
                        continue;
                    if (_world.IsPassable(x + 0.5, y + 0.5, true))
                        tiles.Add((x, y));
                }
            foreach (var (x, y) in tiles.OrderBy(_ => Random.Shared.Next()).Take(GIFT_COUNT))
            {
                var gift = _world.CreateNewEntity(GIFT, x + 0.5f, y + 0.5f);
                if (gift != null)
                    _gifts.Add(gift);
            }
            if (_gifts.Count > 0)
                SayNear($"{_gifts.Count} XP gifts! Shoot them for experience.");
        }

        // each player who comes near hears about /gear, at most once every GREET_AGAIN seconds
        private void Greet()
        {
            foreach (var id in _greeted.Where(g => g.Value <= _clock).Select(g => g.Key).ToList())
                _greeted.Remove(id);
            foreach (var player in _world.Players.Values)
            {
                if (_greeted.ContainsKey(player.Id) || player.SqDistTo(_bot) > GREET_RANGE * GREET_RANGE)
                    continue;
                _greeted[player.Id] = _clock + GREET_AGAIN;
                var line = player.Level < commands.doomed.GearCommand.MAX_LEVEL
                    ? $"Hi {player.Name}! Type /gear for free starter gear, then /realm to go fight. /godlands takes you to the Godlands."
                    : $"Hi {player.Name}! Type /godlands to go straight to the Godlands.";
                player.Client.SendPacket(Speech(line));
            }
        }

        private void SayNear(string text)
        {
            var packet = Speech(text);
            foreach (var player in _world.Players.Values)
                if (player.SqDistTo(_bot) < TALK_RANGE * TALK_RANGE)
                    player.Client.SendPacket(packet);
        }

        private Text Speech(string text) =>
            new Text() { Name = NAME, ObjectId = _bot.Id, NumStars = -1, BubbleTime = 5, Recipient = "", Txt = text };

        // the spot, or the nearest open tile to it if the map has changed
        private (float X, float Y) FindSpot((float X, float Y) spot)
        {
            for (var r = 0; r <= 3; r++)
                for (var dy = -r; dy <= r; dy++)
                    for (var dx = -r; dx <= r; dx++)
                        if (_world.IsPassable(spot.X + dx, spot.Y + dy, true))
                            return (spot.X + dx, spot.Y + dy);
            return spot;
        }
    }
}
