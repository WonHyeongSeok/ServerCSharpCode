using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NephirokServer.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace NephirokServer.DB
{
	public class AppDbContext : DbContext
	{
		public DbSet<AccountDb> Accounts { get; set; }
		public DbSet<PlayerDb> Players { get; set; }
		public DbSet<ItemDb> Items { get; set; }
		public DbSet<MailDb> Mails { get; set; }

		public DbSet<PlayerHistoryDb> PlayerHistories { get; set; }
	
		public DbSet<CollectionDb> Collections { get; set; }
		public DbSet<QuestDb> Quests { get; set; }
		public DbSet<GoldRankingDb> GoldRankigs { get; set; }
		
		static readonly ILoggerFactory _logger = LoggerFactory.Create(builder => { builder.AddConsole(); });

		string _connectionString = "";
		protected override void OnConfiguring(DbContextOptionsBuilder options)
		{
			options
				.UseLoggerFactory(_logger)
				.UseSqlServer(ConfigManager.Config == null ? _connectionString : ConfigManager.Config.connectionString);
		}

		protected override void OnModelCreating(ModelBuilder builder)
		{
			builder.Entity<AccountDb>()
				.HasIndex(a => a.AccountId)
				.IsUnique();

			builder.Entity<PlayerDb>()
				.HasIndex(p => p.PlayerName)
				.IsUnique();

			builder.Entity<MailDb>()
				.HasIndex(p => p.MailEventDbId)
				.IsUnique();
		
		}
	}
}
