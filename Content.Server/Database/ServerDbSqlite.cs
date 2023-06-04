using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.IP;
using Content.Server.Preferences.Managers;
using Content.Shared.CCVar;
using Microsoft.EntityFrameworkCore;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Server.Database
{
    /// <summary>
    ///     Provides methods to retrieve and update character preferences.
    ///     Don't use this directly, go through <see cref="ServerPreferencesManager" /> instead.
    /// </summary>
    public sealed class ServerDbSqlite : ServerDbBase
    {
        private readonly Func<DbContextOptions<SqliteServerDbContext>> _options;

        // This doesn't allow concurrent access so that's what the semaphore is for.
        // That said, this is bloody SQLite, I don't even think EFCore bothers to truly async it.
        private readonly SemaphoreSlim _prefsSemaphore;

        private readonly Task _dbReadyTask;

        private int _msDelay;

        public ServerDbSqlite(Func<DbContextOptions<SqliteServerDbContext>> options, bool inMemory)
        {
            _options = options;

            var prefsCtx = new SqliteServerDbContext(options());

            var cfg = IoCManager.Resolve<IConfigurationManager>();

            // When inMemory we re-use the same connection, so we can't have any concurrency.
            var concurrency = inMemory ? 1 : cfg.GetCVar(CCVars.DatabaseSqliteConcurrency);
            _prefsSemaphore = new SemaphoreSlim(concurrency, concurrency);

            if (cfg.GetCVar(CCVars.DatabaseSynchronous))
            {
                prefsCtx.Database.Migrate();
                _dbReadyTask = Task.CompletedTask;
                prefsCtx.Dispose();
            }
            else
            {
                _dbReadyTask = Task.Run(() =>
                {
                    prefsCtx.Database.Migrate();
                    prefsCtx.Dispose();
                });
            }

            cfg.OnValueChanged(CCVars.DatabaseSqliteDelay, v => _msDelay = v, true);
        }

        #region Ban
        public override async Task<ServerBanDef?> GetServerBanAsync(int id)
        {
            await using var db = await GetDbImpl();

            var ban = await db.SqliteDbContext.Ban
                .Include(p => p.Unban)
                .Where(p => p.Id == id)
                .SingleOrDefaultAsync();

            return ConvertBan(ban);
        }

        public override async Task<ServerBanDef?> GetServerBanAsync(
            IPAddress? address,
            NetUserId? userId,
            ImmutableArray<byte>? hwId)
        {
            await using var db = await GetDbImpl();

            var exempt = await GetBanExemptionCore(db, userId);

            // SQLite can't do the net masking stuff we need to match IP address ranges.
            // So just pull down the whole list into memory.
            var bans = await GetAllBans(db.SqliteDbContext, includeUnbanned: false, exempt);

            return bans.FirstOrDefault(b => BanMatches(b, address, userId, hwId, exempt)) is { } foundBan
                ? ConvertBan(foundBan)
                : null;
        }

        public override async Task<List<ServerBanDef>> GetServerBansAsync(IPAddress? address,
            NetUserId? userId,
            ImmutableArray<byte>? hwId, bool includeUnbanned)
        {
            await using var db = await GetDbImpl();

            var exempt = await GetBanExemptionCore(db, userId);

            // SQLite can't do the net masking stuff we need to match IP address ranges.
            // So just pull down the whole list into memory.
            var queryBans = await GetAllBans(db.SqliteDbContext, includeUnbanned, exempt);

            return queryBans
                .Where(b => BanMatches(b, address, userId, hwId, exempt))
                .Select(ConvertBan)
                .ToList()!;
        }

        private static async Task<List<ServerBan>> GetAllBans(
            SqliteServerDbContext db,
            bool includeUnbanned,
            ServerBanExemptFlags? exemptFlags)
        {
            IQueryable<ServerBan> query = db.Ban.Include(p => p.Unban);
            if (!includeUnbanned)
            {
                query = query.Where(p =>
                    p.Unban == null && (p.ExpirationTime == null || p.ExpirationTime.Value > DateTime.UtcNow));
            }

            if (exemptFlags is { } exempt)
            {
                query = query.Where(b => (b.ExemptFlags & exempt) == 0);
            }

            return await query.ToListAsync();
        }

        private static bool BanMatches(ServerBan ban,
            IPAddress? address,
            NetUserId? userId,
            ImmutableArray<byte>? hwId,
            ServerBanExemptFlags? exemptFlags)
        {
            if (!exemptFlags.GetValueOrDefault(ServerBanExemptFlags.None).HasFlag(ServerBanExemptFlags.IP)
                && address != null && ban.Address is not null && address.IsInSubnet(ban.Address.Value))
            {
                return true;
            }

            if (userId is { } id && ban.PlayerUserId == id.UserId)
            {
                return true;
            }

            return hwId is { Length: > 0 } hwIdVar && hwIdVar.AsSpan().SequenceEqual(ban.HWId);
        }

        public override async Task AddServerBanAsync(ServerBanDef serverBan)
        {
            await using var db = await GetDbImpl();

            db.SqliteDbContext.Ban.Add(new ServerBan
            {
                Address = serverBan.Address,
                Reason = serverBan.Reason,
                Severity = serverBan.Severity,
                BanningAdmin = serverBan.BanningAdmin?.UserId,
                HWId = serverBan.HWId?.ToArray(),
                BanTime = serverBan.BanTime.UtcDateTime,
                ExpirationTime = serverBan.ExpirationTime?.UtcDateTime,
                RoundId = serverBan.RoundId,
                PlaytimeAtNote = serverBan.PlaytimeAtNote,
                PlayerUserId = serverBan.UserId?.UserId
            });

            await db.SqliteDbContext.SaveChangesAsync();
        }

        public override async Task AddServerUnbanAsync(ServerUnbanDef serverUnban)
        {
            await using var db = await GetDbImpl();

            db.SqliteDbContext.Unban.Add(new ServerUnban
            {
                BanId = serverUnban.BanId,
                UnbanningAdmin = serverUnban.UnbanningAdmin?.UserId,
                UnbanTime = serverUnban.UnbanTime.UtcDateTime
            });

            await db.SqliteDbContext.SaveChangesAsync();
        }
        #endregion

        #region Role Ban
        public override async Task<ServerRoleBanDef?> GetServerRoleBanAsync(int id)
        {
            await using var db = await GetDbImpl();

            var ban = await db.SqliteDbContext.RoleBan
                .Include(p => p.Unban)
                .Where(p => p.Id == id)
                .SingleOrDefaultAsync();

            return ConvertRoleBan(ban);
        }

        public override async Task<List<ServerRoleBanDef>> GetServerRoleBansAsync(
            IPAddress? address,
            NetUserId? userId,
            ImmutableArray<byte>? hwId,
            bool includeUnbanned)
        {
            await using var db = await GetDbImpl();

            // SQLite can't do the net masking stuff we need to match IP address ranges.
            // So just pull down the whole list into memory.
            var queryBans = await GetAllRoleBans(db.SqliteDbContext, includeUnbanned);

            return queryBans
                .Where(b => RoleBanMatches(b, address, userId, hwId))
                .Select(ConvertRoleBan)
                .ToList()!;
        }

        private static async Task<List<ServerRoleBan>> GetAllRoleBans(
            SqliteServerDbContext db,
            bool includeUnbanned)
        {
            IQueryable<ServerRoleBan> query = db.RoleBan.Include(p => p.Unban);
            if (!includeUnbanned)
            {
                query = query.Where(p =>
                    p.Unban == null && (p.ExpirationTime == null || p.ExpirationTime.Value > DateTime.UtcNow));
            }

            return await query.ToListAsync();
        }

        private static bool RoleBanMatches(
            ServerRoleBan ban,
            IPAddress? address,
            NetUserId? userId,
            ImmutableArray<byte>? hwId)
        {
            if (address != null && ban.Address is not null && address.IsInSubnet(ban.Address.Value))
            {
                return true;
            }

            if (userId is { } id && ban.PlayerUserId == id.UserId)
            {
                return true;
            }

            return hwId is { Length: > 0 } hwIdVar && hwIdVar.AsSpan().SequenceEqual(ban.HWId);
        }

        public override async Task AddServerRoleBanAsync(ServerRoleBanDef serverBan)
        {
            await using var db = await GetDbImpl();

            db.SqliteDbContext.RoleBan.Add(new ServerRoleBan
            {
                Address = serverBan.Address,
                Reason = serverBan.Reason,
                Severity = serverBan.Severity,
                BanningAdmin = serverBan.BanningAdmin?.UserId,
                HWId = serverBan.HWId?.ToArray(),
                BanTime = serverBan.BanTime.UtcDateTime,
                ExpirationTime = serverBan.ExpirationTime?.UtcDateTime,
                RoundId = serverBan.RoundId,
                PlaytimeAtNote = serverBan.PlaytimeAtNote,
                PlayerUserId = serverBan.UserId?.UserId,
                RoleId = serverBan.Role,
            });

            await db.SqliteDbContext.SaveChangesAsync();
        }

        public override async Task AddServerRoleUnbanAsync(ServerRoleUnbanDef serverUnban)
        {
            await using var db = await GetDbImpl();

            db.SqliteDbContext.RoleUnban.Add(new ServerRoleUnban
            {
                BanId = serverUnban.BanId,
                UnbanningAdmin = serverUnban.UnbanningAdmin?.UserId,
                UnbanTime = serverUnban.UnbanTime.UtcDateTime
            });

            await db.SqliteDbContext.SaveChangesAsync();
        }

        private static ServerRoleBanDef? ConvertRoleBan(ServerRoleBan? ban)
        {
            if (ban == null)
            {
                return null;
            }

            NetUserId? uid = null;
            if (ban.PlayerUserId is { } guid)
            {
                uid = new NetUserId(guid);
            }

            NetUserId? aUid = null;
            if (ban.BanningAdmin is { } aGuid)
            {
                aUid = new NetUserId(aGuid);
            }

            var unban = ConvertRoleUnban(ban.Unban);

            return new ServerRoleBanDef(
                ban.Id,
                uid,
                ban.Address,
                ban.HWId == null ? null : ImmutableArray.Create(ban.HWId),
                // SQLite apparently always reads DateTime as unspecified, but we always write as UTC.
                DateTime.SpecifyKind(ban.BanTime, DateTimeKind.Utc),
                ban.ExpirationTime == null ? null : DateTime.SpecifyKind(ban.ExpirationTime.Value, DateTimeKind.Utc),
                ban.RoundId,
                ban.PlaytimeAtNote,
                ban.Reason,
                ban.Severity,
                aUid,
                unban,
                ban.RoleId);
        }

        private static ServerRoleUnbanDef? ConvertRoleUnban(ServerRoleUnban? unban)
        {
            if (unban == null)
            {
                return null;
            }

            NetUserId? aUid = null;
            if (unban.UnbanningAdmin is { } aGuid)
            {
                aUid = new NetUserId(aGuid);
            }

            return new ServerRoleUnbanDef(
                unban.Id,
                aUid,
                // SQLite apparently always reads DateTime as unspecified, but we always write as UTC.
                DateTime.SpecifyKind(unban.UnbanTime, DateTimeKind.Utc));
        }
        #endregion

        protected override PlayerRecord MakePlayerRecord(Player record)
        {
            return new PlayerRecord(
                new NetUserId(record.UserId),
                new DateTimeOffset(record.FirstSeenTime, TimeSpan.Zero),
                record.LastSeenUserName,
                new DateTimeOffset(record.LastSeenTime, TimeSpan.Zero),
                record.LastSeenAddress,
                record.LastSeenHWId?.ToImmutableArray());
        }

        private static ServerBanDef? ConvertBan(ServerBan? ban)
        {
            if (ban == null)
            {
                return null;
            }

            NetUserId? uid = null;
            if (ban.PlayerUserId is { } guid)
            {
                uid = new NetUserId(guid);
            }

            NetUserId? aUid = null;
            if (ban.BanningAdmin is { } aGuid)
            {
                aUid = new NetUserId(aGuid);
            }

            var unban = ConvertUnban(ban.Unban);

            return new ServerBanDef(
                ban.Id,
                uid,
                ban.Address,
                ban.HWId == null ? null : ImmutableArray.Create(ban.HWId),
                // SQLite apparently always reads DateTime as unspecified, but we always write as UTC.
                DateTime.SpecifyKind(ban.BanTime, DateTimeKind.Utc),
                ban.ExpirationTime == null ? null : DateTime.SpecifyKind(ban.ExpirationTime.Value, DateTimeKind.Utc),
                ban.RoundId,
                ban.PlaytimeAtNote,
                ban.Reason,
                ban.Severity,
                aUid,
                unban);
        }

        private static ServerUnbanDef? ConvertUnban(ServerUnban? unban)
        {
            if (unban == null)
            {
                return null;
            }

            NetUserId? aUid = null;
            if (unban.UnbanningAdmin is { } aGuid)
            {
                aUid = new NetUserId(aGuid);
            }

            return new ServerUnbanDef(
                unban.Id,
                aUid,
                // SQLite apparently always reads DateTime as unspecified, but we always write as UTC.
                DateTime.SpecifyKind(unban.UnbanTime, DateTimeKind.Utc));
        }

        public override async Task<int>  AddConnectionLogAsync(
            NetUserId userId,
            string userName,
            IPAddress address,
            ImmutableArray<byte> hwId,
            ConnectionDenyReason? denied)
        {
            await using var db = await GetDbImpl();

            var connectionLog = new ConnectionLog
            {
                Address = address,
                Time = DateTime.UtcNow,
                UserId = userId.UserId,
                UserName = userName,
                HWId = hwId.ToArray(),
                Denied = denied
            };

            db.SqliteDbContext.ConnectionLog.Add(connectionLog);

            await db.SqliteDbContext.SaveChangesAsync();

            return connectionLog.Id;
        }

        public override async Task<((Admin, string? lastUserName)[] admins, AdminRank[])> GetAllAdminAndRanksAsync(
            CancellationToken cancel)
        {
            await using var db = await GetDbImpl();

            var admins = await db.SqliteDbContext.Admin
                .Include(a => a.Flags)
                .GroupJoin(db.SqliteDbContext.Player, a => a.UserId, p => p.UserId, (a, grouping) => new {a, grouping})
                .SelectMany(t => t.grouping.DefaultIfEmpty(), (t, p) => new {t.a, p!.LastSeenUserName})
                .ToArrayAsync(cancel);

            var adminRanks = await db.DbContext.AdminRank.Include(a => a.Flags).ToArrayAsync(cancel);

            return (admins.Select(p => (p.a, p.LastSeenUserName)).ToArray(), adminRanks)!;
        }

        public override async Task<int> AddNewRound(Server server, params Guid[] playerIds)
        {
            await using var db = await GetDb();

            var players = await db.DbContext.Player
                .Where(player => playerIds.Contains(player.UserId))
                .ToListAsync();

            var nextId = 1;
            if (await db.DbContext.Round.AnyAsync())
            {
                nextId = db.DbContext.Round.Max(round => round.Id) + 1;
            }

            var round = new Round
            {
                Id = nextId,
                Players = players,
                ServerId = server.Id
            };

            db.DbContext.Round.Add(round);

            await db.DbContext.SaveChangesAsync();

            return round.Id;
        }

        public override async Task<int> AddAdminNote(AdminNote note)
        {
            await using (var db = await GetDb())
            {
                var nextId = 1;
                if (await db.DbContext.AdminNotes.AnyAsync())
                {
                    nextId = await db.DbContext.AdminNotes.MaxAsync(adminNote => adminNote.Id) + 1;
                }

                note.Id = nextId;
            }

            return await base.AddAdminNote(note);
        }
        public override async Task<int> AddAdminWatchlist(AdminWatchlist watchlist)
        {
            await using (var db = await GetDb())
            {
                var nextId = 1;
                if (await db.DbContext.AdminWatchlists.AnyAsync())
                {
                    nextId = await db.DbContext.AdminWatchlists.MaxAsync(adminWatchlist => adminWatchlist.Id) + 1;
                }

                watchlist.Id = nextId;
            }

            return await base.AddAdminWatchlist(watchlist);
        }

        public override async Task<int> AddAdminMessage(AdminMessage message)
        {
            await using (var db = await GetDb())
            {
                var nextId = 1;
                if (await db.DbContext.AdminMessages.AnyAsync())
                {
                    nextId = await db.DbContext.AdminMessages.MaxAsync(adminMessage => adminMessage.Id) + 1;
                }

                message.Id = nextId;
            }

            return await base.AddAdminMessage(message);
        }

        private async Task<DbGuardImpl> GetDbImpl()
        {
            await _dbReadyTask;
            if (_msDelay > 0)
                await Task.Delay(_msDelay);

            await _prefsSemaphore.WaitAsync();

            var dbContext = new SqliteServerDbContext(_options());

            return new DbGuardImpl(this, dbContext);
        }

        protected override async Task<DbGuard> GetDb()
        {
            return await GetDbImpl().ConfigureAwait(false);
        }

        private sealed class DbGuardImpl : DbGuard
        {
            private readonly ServerDbSqlite _db;
            private readonly SqliteServerDbContext _ctx;

            public DbGuardImpl(ServerDbSqlite db, SqliteServerDbContext dbContext)
            {
                _db = db;
                _ctx = dbContext;
            }

            public override ServerDbContext DbContext => _ctx;
            public SqliteServerDbContext SqliteDbContext => _ctx;

            public override async ValueTask DisposeAsync()
            {
                await _ctx.DisposeAsync();
                _db._prefsSemaphore.Release();
            }
        }
    }
}
