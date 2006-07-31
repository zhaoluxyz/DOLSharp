/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */
using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using DOL.AI.Brain;
using DOL.Database;
using DOL.GS.PacketHandler;
using log4net;

namespace DOL.GS
{
	/// <summary>
	/// Represents an in-game merchant
	/// </summary>
	public class GameMerchant : GameNPC
	{
		/// <summary>
		/// Defines a logger for this class.
		/// </summary>
		private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		/// <summary>
		/// Constructor
		/// </summary>
		public GameMerchant() : base()
		{
		}

		#region GetExamineMessages / Interact / Whisp

		/// <summary>
		/// Adds messages to ArrayList which are sent when object is targeted
		/// </summary>
		/// <param name="player">GamePlayer that is examining this object</param>
		/// <returns>list with string messages</returns>
		public override IList GetExamineMessages(GamePlayer player)
		{
			IList list = base.GetExamineMessages(player);
			list.Add("You examine " + GetName(0, false) + ".  " + GetPronoun(0, true) + " is " + GetAggroLevelString(player, false) + " and is a merchant.");
			list.Add("[Right click to display a shop window]");
			return list;
		}

		/// <summary>
		/// Called when a player right clicks on the merchant
		/// </summary>
		/// <param name="player">Player that interacted with the merchant</param>
		/// <returns>True if succeeded</returns>
		public override bool Interact(GamePlayer player)
		{
			if (!base.Interact(player))
				return false;
			TurnTo(player, 10000);
			SendMerchantWindow(player);
//			player.Out.SendMessage(
//				"Hello "+player.Name+", Do you want to [trade] with me ?",
//				eChatType.CT_System,eChatLoc.CL_PopupWindow);
			return true;
		}

		/// <summary>
		/// send the merchants item offer window to a player
		/// </summary>
		/// <param name="player"></param>
		public virtual void SendMerchantWindow(GamePlayer player) {
			ThreadPool.QueueUserWorkItem(new WaitCallback(SendMerchatWindowCallback), player);
		}

		/// <summary>
		/// Sends merchant window from threadpool thread
		/// </summary>
		/// <param name="state">The game player to send to</param>
		protected virtual void SendMerchatWindowCallback(object state)
		{
			((GamePlayer)state).Out.SendMerchantWindow(m_tradeItems, eMerchantWindowType.Normal);
		}

		/// <summary>
		/// Whispers a reply
		/// </summary>
		/// <param name="source">Source of the trade</param>
		/// <param name="str">Type of interaction requested</param>
		/// <returns>True if succeeded</returns>
		public override bool WhisperReceive(GameLiving source, string str)
		{
			if (!base.WhisperReceive(source, str))
				return false;
			if (source == null || !(source is GamePlayer))
				return false;
			GamePlayer t = (GamePlayer) source;
			switch (str)
			{
				case "trade":
					t.Out.SendMessage("You can buy the items I have available. I'm not really interested in your items, but I'll purchase them for an average price!", eChatType.CT_System, eChatLoc.CL_PopupWindow);
					break;
				default:
					break;
			}
			return true;
		}

		#endregion

		#region Items List

		/// <summary>
		/// Items available for sale
		/// </summary>
		protected MerchantTradeItems m_tradeItems;

		/// <summary>
		/// Gets the items available from this merchant
		/// </summary>
		public MerchantTradeItems TradeItems
		{
			get { return m_tradeItems; }
			set { m_tradeItems = value; }
		}

		#endregion

		#region Buy / Sell / Apparaise

		/// <summary>
		/// Called when a player buys an item
		/// </summary>
		/// <param name="player">The player making the purchase</param>
		/// <param name="item_slot">slot of the item to be bought</param>
		/// <param name="number">Number to be bought</param>
		/// <returns>true if buying is allowed, false if buying should be prevented</returns>
		public virtual bool OnPlayerBuy(GamePlayer player, int item_slot, int number)
		{
			return true;
		}

		/// <summary>
		/// Called when a player sells something
		/// </summary>
		/// <param name="player">Player making the sale</param>
		/// <param name="item">The InventoryItem to be sold</param>
		/// <returns>true if selling is allowed, false if it should be prevented</returns>
		public virtual bool OnPlayerSell(GamePlayer player, InventoryItem item)
		{
			if (!item.IsDropable)
			{
				player.Out.SendMessage("This item can't be sold.", eChatType.CT_Merchant, eChatLoc.CL_SystemWindow);
				return false;
			}

			if(!WorldMgr.CheckDistance(this, player, WorldMgr.PICKUP_DISTANCE)) // tested
			{ 
				player.Out.SendMessage(Name +" is too far away!", eChatType.CT_Merchant, eChatLoc.CL_SystemWindow);
				return false;
			}

			return true;
		}

		/// <summary>
		/// Called to appraise the value of an item
		/// </summary>
		/// <param name="player">The player whose item needs appraising</param>
		/// <param name="item">The item to be appraised</param>
		/// <returns>The price this merchant will pay for the offered items</returns>
		public virtual long OnPlayerAppraise(GamePlayer player, InventoryItem item)
		{
			if (item == null)
				return 0;

			int itemCount = Math.Max(1, item.Count);
			int packSize = Math.Max(1, item.PackSize);
			return item.Value*itemCount/packSize/2;
		}

		#endregion

		#region Database

		/// <summary>
		/// Loads a merchant from the DB
		/// </summary>
		/// <param name="merchantobject">The merchant DB object</param>
		public override void LoadFromDatabase(DataObject merchantobject)
		{
			base.LoadFromDatabase(merchantobject);
			if(!(merchantobject is Mob)) return;
			Mob merchant = (Mob) merchantobject;
			if (merchant.ItemsListTemplateID != null && merchant.ItemsListTemplateID.Length > 0)
				m_tradeItems = new MerchantTradeItems(merchant.ItemsListTemplateID);
		}

		/// <summary>
		/// Saves a merchant into the DB
		/// </summary>
		public override void SaveIntoDatabase()
		{
			Mob merchant = null;
			if (InternalID != null)
				merchant = (Mob) GameServer.Database.FindObjectByKey(typeof (Mob), InternalID);
			if (merchant == null)
				merchant = new Mob();

			merchant.Name = Name;
			merchant.Guild = GuildName;
			merchant.X = X;
			merchant.Y = Y;
			merchant.Z = Z;
			merchant.Heading = Heading;
			merchant.Speed = MaxSpeedBase;
			merchant.Region = CurrentRegionID;
			merchant.Realm = Realm;
			merchant.Model = Model;
			merchant.Size = Size;
			merchant.Level = Level;
			merchant.Flags = Flags;
			IAggressiveBrain aggroBrain = Brain as IAggressiveBrain;
			if (aggroBrain != null)
			{
				merchant.AggroLevel = aggroBrain.AggroLevel;
				merchant.AggroRange = aggroBrain.AggroRange;
			}
			merchant.ClassType = this.GetType().ToString();
			merchant.EquipmentTemplateID = EquipmentTemplateID;
			if (m_tradeItems == null)
			{
				merchant.ItemsListTemplateID = null;
			}
			else
			{
				merchant.ItemsListTemplateID = m_tradeItems.ItemsListID;
			}

			if (InternalID == null)
			{
				GameServer.Database.AddNewObject(merchant);
				InternalID = merchant.ObjectId;
			}
			else
			{
				GameServer.Database.SaveObject(merchant);
			}
		}

		/// <summary>
		/// Deletes a merchant from the DB
		/// </summary>
		public override void DeleteFromDatabase()
		{
			if (InternalID != null)
			{
				Mob merchant = (Mob) GameServer.Database.FindObjectByKey(typeof (Mob), InternalID);
				if (merchant != null)
					GameServer.Database.DeleteObject(merchant);
			}
			InternalID = null;
		}

		#endregion
	}

	/* 
 * Author:   Avithan 
 * Date:   22.12.2005 
 * Bounty merchant 
 */ 

	public class GameBountyMerchant : GameMerchant
	{
		protected override void SendMerchatWindowCallback(object state)
		{
			((GamePlayer)state).Out.SendMerchantWindow(m_tradeItems, eMerchantWindowType.Bp);
		}
	}

	public class GameChampionMerchant : GameMerchant
	{
		public override bool OnPlayerBuy(GamePlayer player, int item_slot, int number)
		{
			/*
			int page = item_slot / MerchantTradeItems.MAX_ITEM_IN_TRADEWINDOWS;
			if (player.ChampionLevel >= page + 2)
				return base.OnPlayerBuy(player, item_slot, number);
			else
			{
				player.Out.SendMessage("You must be Champion Level " + (page + 2) + " or higher to be able to buy this horse!", eChatType.CT_Say, eChatLoc.CL_PopupWindow);
				return false;
			}
			 */
			return false;
		}

	}

	public abstract class GameCountMerchant : GameMerchant
	{
		protected GameInventoryItem m_moneyItem;

		public GameInventoryItem moneyItem
		{
			get { return m_moneyItem; }
		}

		public override IList GetExamineMessages(GamePlayer player)
		{
			IList list = base.GetExamineMessages(player);
			list.Add("You examine " + GetName(0, false) + ".  " + GetPronoun(0, true) + " is " + GetAggroLevelString(player, false) + " and is a merchant.");
			list.Add("You can buy items here for " + moneyItem.Item.Name + ".");
			list.Add("[Right click to display a shop window]");
			return list;
		}

		protected override void SendMerchatWindowCallback(object state)
		{
			((GamePlayer)state).Out.SendMerchantWindow(m_tradeItems, eMerchantWindowType.Count);
		}
	}

	public class GameDiamondSealsMerchant : GameCountMerchant
	{
		public override bool AddToWorld()
		{
			if (!base.AddToWorld())
				return false;
			m_moneyItem = GameInventoryItem.CreateFromTemplate("dias");
			return true;
		}
	}

	public class GameSapphireSealsMerchant : GameCountMerchant
	{
		public override bool AddToWorld()
		{
			if (!base.AddToWorld())
				return false;
			m_moneyItem = GameInventoryItem.CreateFromTemplate("saphir");
			return true;
		}

	}

	public class GameEmeraldSealsMerchant : GameCountMerchant
	{
		public override bool AddToWorld()
		{
			if (!base.AddToWorld())
				return false;
			m_moneyItem = GameInventoryItem.CreateFromTemplate("smaras");
			return true;
		}

	}

	public class GameAuruliteMerchant : GameCountMerchant
	{
		public override bool AddToWorld()
		{
			if (!base.AddToWorld())
				return false;
			m_moneyItem = GameInventoryItem.CreateFromTemplate("aurulite");
			return true;
		}

	}
}