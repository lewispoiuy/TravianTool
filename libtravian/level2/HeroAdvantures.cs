﻿/*
 * Created by SharpDevelop.
 * User: Administrator
 * Date: 2011-5-1
 * Time: 11:18
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using LitJson;

namespace libTravian
{
	partial class Travian
	{
		enum HeroStatus
		{
			HERO_NOT_BELONG_HERE,
			HERO_IN_ADVANTURE,
			HERO_NOT_IN_ADVANTURE
		};
		private HeroStatus CheckIfInAdventure(int VillageID)
		{
			TVillage CV = TD.Villages[VillageID];
			
			HeroStatus status = HeroStatus.HERO_NOT_BELONG_HERE;
			foreach (TTInfo info in CV.Troop.Troops)
			{
				if (info.OwnerVillageZ != CV.Z || info.Troops[10] != 1)
					continue;
				if (TD.Adv_Sta.HeroLocate != VillageID)
				{
					TD.Adv_Sta.HeroLocate = VillageID;
					DebugLog("探测到英雄所在村，更正为：" + CV.Name 
					         + "(" + VillageID.ToString() + ")", DebugLevel.II);
				}
				if (info.VillageName.Contains("英雄冒险") 
				    && info.TroopType == TTroopType.Outgoing)
					status = HeroStatus.HERO_IN_ADVANTURE;
				else
					status = HeroStatus.HERO_NOT_IN_ADVANTURE;
				break;
			}
			return status;
		}
		
		public void doFetchHeroAdventures(object o)
        {
        	lock (Level2Lock)
            {
                int VillageID = (int)o;
                string data = PageQuery(VillageID, "hero_inventory.php");	//	查询英雄状态
                
                if (string.IsNullOrEmpty(data))
                    return;
                
                string hero_status = HtmlUtility.GetElementWithClass(
                	data, "div", "attribute heroStatus");
                if (string.IsNullOrEmpty(hero_status))
                	return;
                Match m = Regex.Match(hero_status, "karte.php\\?d=(\\d+)");
                int hero_loc = 0;
                if (m.Success)
                {
                	int z = Convert.ToInt32(m.Groups[1].Value);
                	foreach (var x in TD.Villages)
                	{
                		TVillage v = x.Value;
                		if (v.Z == z)
                		{
                			hero_loc = x.Key;
                			TD.Adv_Sta.HeroLocate = hero_loc;
                			break;
                		}
                	}
                }
                else
                {
                	hero_loc = (TD.Adv_Sta.HeroLocate == 0 ? VillageID : TD.Adv_Sta.HeroLocate);
                }
                
                data = PageQuery(hero_loc, "hero_adventure.php");	//	查询探险地点
                if (string.IsNullOrEmpty(data))
                    return;
                string[] places = HtmlUtility.GetElements(data, "tr");
                if (places.Length <= 1)
                	return;
				
                int coord_x, coord_y;
                string dur, dgr, lnk;
                DateTime fin;
                TD.Adv_Sta.HeroAdventures.Clear();
                for (int i = 1; i < places.Length; i++)
                {
                	//	坐标
                	string coords = HtmlUtility.GetElementWithClass(
                		places[i], "td", "coords");
                	if (coords == null)
                		continue;
                	m = Regex.Match(coords, "karte.php\\?x=(\\-?\\d+)&amp;y=(\\-?\\d+)");
                	if (!m.Success)
                		continue;
                	coord_x = Convert.ToInt32(m.Groups[1].Value);
                	coord_y = Convert.ToInt32(m.Groups[2].Value);
                	
                	//	持续时间
                	string move_time = HtmlUtility.GetElementWithClass(
                		places[i], "td", "moveTime");
                	if (move_time == null)
                		continue;
                	m = Regex.Match(move_time, "\\d+:\\d+:\\d+");
                	if (!m.Success)
                		continue;
                	dur = m.Groups[0].Value;
                	
                	//	难度
                	string difficulty = HtmlUtility.GetElementWithClass(
                		places[i], "td", "difficulty");
                	if (difficulty == null)
                		continue;
                	m = Regex.Match(difficulty, "alt=\"([^\"]*?)\"");
                	if (!m.Success)
                		continue;
                	dgr = m.Groups[1].Value;
                	
                	//	难度
                	string timeLeft = HtmlUtility.GetElementWithClass(
                		places[i], "td", "timeLeft");
                	if (timeLeft == null)
                		continue;
                	m = Regex.Match(timeLeft, "\\d+:\\d+:\\d+");
                	if (!m.Success)
                		continue;
                	fin = DateTime.Now.Add(TimeSpanParse(m.Groups[0].Value));
                	
                	//	链接
                	string goTo = HtmlUtility.GetElementWithClass(
                		places[i], "td", "goTo");
                	if (goTo == null)
                		continue;
                	m = Regex.Match(goTo, "href=\"([^\"]*?)\"");
                	if (!m.Success)
                		continue;
                	lnk = m.Groups[1].Value;
                	
                	//	增加新的探险地点
                	HeroAdventureInfo adv_info = new HeroAdventureInfo()
                	{
                		axis_x = coord_x,
                		axis_y = coord_y,
                		duration = dur,
                		danger = dgr,
                		finish_time = fin,
                		link = lnk
                	};
                	TD.Adv_Sta.HeroAdventures.Add(adv_info);
                }
                
                TD.Adv_Sta.bIsHeroAdventureInitialize = true;
                TD.Adv_Sta.bShouldRefreshAdventureDisplay = true;
                TD.Dirty = true;
        	}
        }
		
		private void doHeroAdventure(object o)
		{
			lock (Level2Lock)
            {
				int HeroLoc = TD.Adv_Sta.HeroLocate;
				int Key = (int)o;

				if (Key <  0 || Key >= TD.Adv_Sta.HeroAdventures.Count)
				{
					return;
				}
				TPoint tp = new TPoint(TD.Adv_Sta.HeroAdventures[Key].axis_x, TD.Adv_Sta.HeroAdventures[Key].axis_y);
				string data = PageQuery(HeroLoc, "start_adventure.php?from=list&kid=" + tp.Z.ToString());
				
				if (string.IsNullOrEmpty(data))
                    return;
                Match m_test = Regex.Match(data, "type=\"submit\" value=\".*?\" name=\"start\"");
                if (!m_test.Success)
                {
                	DebugLog("英雄目前还无法进行探险！", DebugLevel.II);
					return;
                }
				Dictionary<string, string> PostData = new Dictionary<string, string>();
				MatchCollection mc = Regex.Matches(
					data, "<input type=\"hidden\" name=\"([^\"]*?)\" value=\"([^\"]*?)\" />");
				string key, val;
				foreach (Match m in mc)
				{
					key = m.Groups[1].Value;
					val = m.Groups[2].Value;
					PostData[key] = val;
				}
				PageQuery(HeroLoc, "start_adventure.php", PostData);
				PageQuery(HeroLoc, "build.php?gid=16&tt=1");
			}
		}
	}
}