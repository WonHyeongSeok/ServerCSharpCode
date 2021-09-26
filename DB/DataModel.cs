using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace NephirokServer.DB
{

	[Table("Account")]
	public class AccountDb
	{
		public int AccountDbId { get; set; }
		public string AccountId { get; set; }

		public PlayerDb Player { get; set; }
	}

	[Table("Player")]
	public class PlayerDb
	{
		public int PlayerDbId { get; set; }
		public string PlayerName { get; set; }
		public long PlayerGold { get; set; }
		public int PlayerDia { get; set; }
		public int Level { get; set; }
		public int RankPoint { get; set; }

		public int ClassicWinCount { get; set; }
		public int ClassicLoseCount { get; set; }
		public int WildWinCount { get; set; }
		public int WildLoseCount { get; set; }
		public int RankWinCount { get; set; }
		public int RankLoseCount { get; set; }
		
		public int AdsCount { get; set; }

		public int TotalExp { get; set; }

		public bool IsOn { get; set; }
		public bool IsSessionClosed { get; set; }

		[ForeignKey("Account")]
		public int AccountDbId { get; set; }
		public AccountDb Account { get; set; }

		public ICollection<ItemDb> Items { get; set; }
		public ICollection<MailDb> Mails { get; set; }
		public ICollection<QuestDb> Quests { get; set; }
		public ICollection<CollectionDb> CardCollections { get; set; }
		public ICollection<FriendDb> Friends { get; set; }

		public PlayerHistoryDb PlayerHistory { get; set; }
	} 


	[Table("Item")]
	public class ItemDb
	{
		public int ItemDbId { get; set; }
		public int TemplateId { get; set; }
		public int Count { get; set; }
		public int Slot { get; set; }
		public bool Equipped { get; set; } = false;

		[ForeignKey("Owner")]
		public int? OwnerDbId { get; set; }
		public PlayerDb Owner { get; set; }
	}


	[Table("Mail")] //각 케릭터가 가지고 있음.
	public class MailDb
	{
		public int MailDbId { get; set; }
		public int MailEventDbId { get; set; }
		public bool IsRecieved { get; set; }
		public int AttachedTemplateId { get; set; }
		public int AttachedItemCount { get; set; }
		public bool SoftDeleted { get; set; }

		public DateTime RegDate { get; set; }
		public DateTime OpenDate { get; set; }

		[ForeignKey("Owner")]
		public int? OwnerDbId { get; set; }
		public PlayerDb Owner { get; set; }
	
	}




	[Table("Quest")]
	public class QuestDb
	{
		public int QuestDbId { get; set; }
		public int QuestType { get; set; }
		public int QuestId { get; set; }
		public int ProcessCount { get; set; }
		public bool IsCompleted { get; set; }
		public byte State { get; set; }

		public DateTime RegDate { get; set; } // 등록 데이트
		public DateTime LastUpdateDate { get; set; } // 이퀘스트 마지막 진행 날짜
		public DateTime CompleteDate { get; set; } // 이 퀘스트 영구 완료 했니?
		public DateTime ReserveDate { get; set; } // 이 퀘스트 리셋 예약 날짜

		[ForeignKey("Owner")]
		public int? OwnerDbId { get; set; }
		public PlayerDb Owner { get; set; }
	}


	[Table("Collection")]
	public class CollectionDb
	{
		public int CollectionDbId { get; set; }
		public int CollectionTemplateId { get; set; }
		public int ProcessValue { get; set; } //processvalue로바꾸자.
		public byte State { get; set; } // 0안모음 //1 다모음 // 2진행중
		public bool Equipped { get; set; }

		public DateTime RegDate { get; set; } // 등록 데이트
		public DateTime LastUpdateDate { get; set; } // 이퀘스트 마지막 진행 날짜
		public DateTime CompleteDate { get; set; } // 이 퀘스트 영구 완료 했니?

		[ForeignKey("Owner")]
		public int? OwnerDbId { get; set; }
		public PlayerDb Owner { get; set; }
	}

	[Table("Friend")]
	public class FriendDb
	{
		public int FriendDbId { get; set; }
		public int TargetPlayerId { get; set; }
		public int ProgressValue { get; set; }
		public int State { get; set; }

		public DateTime RegDate { get; set; } // 등록 데이트
		public DateTime LastUpdateDate { get; set; } // 이퀘스트 마지막 진행 날짜
		public DateTime CompleteDate { get; set; } // 이 퀘스트 영구 완료 했니?

		[ForeignKey("Owner")]
		public int? OwnerDbId { get; set; }
		public PlayerDb Owner { get; set; }
	}

	[Table("PlayerHistory")]
	public class PlayerHistoryDb
	{
		public int PlayerHistoryDbId { get; set; }
		public int LastObjectId { get; set; }
		public int LastRoomId { get; set; }

		public DateTime LastUpdateDate { get; set; } // 이퀘스트 마지막 진행 날짜

		[ForeignKey("Owner")]
		public int? OwnerDbId { get; set; }
		public PlayerDb Owner { get; set; }
	}


	[Table("GoldRanking")]
	public class GoldRankingDb
	{
		public int GoldRankingDbId { get; set; }
		public string PlayerName { get; set; }
		public long Count { get; set; }	
		public DateTime UpdateDate { get; set; } // 등록 데이트

		[ForeignKey("Owner")]
		public int? OwnerDbId { get; set; }
		public PlayerDb Owner { get; set; }
	}

}
