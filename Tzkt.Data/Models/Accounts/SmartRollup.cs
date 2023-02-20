﻿using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Tzkt.Data.Models
{
    public class SmartRollup : Account
    {
        [Column("CreatorId")]
        public int CreatorId { get; set; }
        public PvmKind PvmKind { get; set; }
        public string Genesis { get; set; }
        public string Commitment { get; set; }
        public int InboxLevel { get; set; }
        public int TotalCommitments { get; set; }
        public int CementedCommitments { get; set; }
        public int ActiveGames { get; set; }
    }

    public enum PvmKind
    {
        Arith,
        Wasm
    }

    public static class SmartRollupModel
    { 
        public static void BuildSmartRollupModel(this ModelBuilder modelBuilder)
        {
            #region indexes
            modelBuilder.Entity<SmartRollup>()
                .HasIndex(x => x.CreatorId);
            #endregion
        }
    }
}