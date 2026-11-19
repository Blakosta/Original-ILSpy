using System;
using System.Collections.Generic;
using System.Globalization;

namespace Skyline_Sports_Project.Engine;

public static class Views
{
	private class BoxColumn
	{
		public string Title;

		public Func<int[], string> Cell;

		public BoxColumn(string title, Func<int[], string> cell)
		{
			Title = title;
			Cell = cell;
		}
	}

	private class BoxGroup
	{
		public string Title;

		public Func<Player, int[], bool> Shows;

		public Func<int[], double> Order;

		public BoxColumn[] Columns;
	}

	private class OfficeCard
	{
		public string Title;

		public string Tab;

		public string Button;

		public bool Urgent;

		public List<string> Lines = new List<string>();
	}

	private const int MaxTransferTargets = 160;

	public static string VenueSection(SportsWorld world, int venueId, string buildingName, string message, int now)
	{
		JsonBuilder j = new JsonBuilder();
		j.BeginObject();
		Venue v = world.GetVenue(venueId);
		j.Prop("isVenue", v != null);
		j.Prop("buildingName", buildingName ?? "");
		j.Prop("message", message ?? "");
		j.Prop("dateLabel", GameClock.DayLabel(GameClock.DayOf(now)));
		if (v != null)
		{
			List<Team> teams = world.TeamsAtVenue(v.Id);
			j.Name("venue").BeginObject();
			j.Prop("id", v.Id);
			j.Prop("type", (int)v.Type);
			j.Prop("typeLabel", SportRules.VenueTypeLabel(v.Type));
			j.Prop("level", (int)v.Level);
			j.Prop("levelLabel", SportRules.LevelLabel(v.Level));
			j.Prop("canChangeLevel", value: true);
			j.Name("types").BeginArray();
			VenueType[] allVenueTypes = SportRules.AllVenueTypes;
			foreach (VenueType type in allVenueTypes)
			{
				bool enabled = true;
				foreach (Team t in teams)
				{
					if (!SportRules.IsAllowed(type, t.Sport))
					{
						enabled = false;
					}
				}
				j.BeginObject().Prop("id", (int)type).Prop("label", SportRules.VenueTypeLabel(type))
					.Prop("sports", SportsList(type))
					.Prop("enabled", enabled)
					.EndObject();
			}
			j.EndArray();
			j.Name("levels").BeginArray();
			Level[] venueLevels = SportRules.VenueLevels;
			Sport[] array;
			foreach (Level level in venueLevels)
			{
				bool fits = false;
				array = SportRules.AllowedSports(v.Type);
				for (int k = 0; k < array.Length; k++)
				{
					if (SportRules.LevelAllowed(array[k], level))
					{
						fits = true;
					}
				}
				if (fits)
				{
					j.BeginObject().Prop("id", (int)level).Prop("label", SportRules.LevelLabel(level))
						.EndObject();
				}
			}
			j.EndArray();
			j.Name("teams").BeginArray();
			foreach (Team t2 in teams)
			{
				WriteTeamSummary(j, world, t2, now);
			}
			j.EndArray();
			int load = world.VenueLoad(v.Id);
			int capacity = SportRules.HomeDateCapacity(v.Type);
			j.Prop("capacityLabel", "Schedule: " + load + " of " + capacity + " home dates used, " + teams.Count + " of " + SportRules.MaxTeams(v.Type) + " teams" + ((load > capacity) ? " (over capacity: some home games move to the other team's venue or a neutral site)" : ""));
			j.Name("addable").BeginArray();
			List<KeyValuePair<string, string>> full = new List<KeyValuePair<string, string>>();
			array = SportRules.AllowedSports(v.Type);
			foreach (Sport sport in array)
			{
				if (!SportRules.LevelAllowed(sport, v.Level))
				{
					continue;
				}
				if (!world.CanHost(v, sport, v.Level, -1, out var why))
				{
					List<string> fits2 = new List<string>();
					venueLevels = SportRules.VenueLevels;
					foreach (Level other in venueLevels)
					{
						if (other != v.Level && world.CanHost(v, sport, other, -1, out var _))
						{
							fits2.Add(SportRules.LevelLabel(other));
						}
					}
					if (fits2.Count > 0)
					{
						why = why + " A " + string.Join(" or ", fits2.ToArray()) + " " + SportRules.SportLabel(sport) + " team still fits (pick it under Level for new teams).";
					}
					full.Add(new KeyValuePair<string, string>(SportRules.LevelLabel(v.Level) + " " + SportRules.SportLabel(sport), why));
				}
				else
				{
					j.BeginObject().Prop("id", (int)sport).Prop("label", SportRules.SportLabel(sport))
						.Prop("dates", SportsWorld.HomeDates(sport, v.Level))
						.EndObject();
				}
			}
			j.EndArray();
			j.Prop("fullNote", (full.Count > 0) ? (full[0].Key + ": " + full[0].Value) : "");
			j.Name("blocked").BeginArray();
			foreach (KeyValuePair<string, string> kv in full)
			{
				j.BeginObject().Prop("label", kv.Key).Prop("reason", kv.Value)
					.EndObject();
			}
			j.EndArray();
			j.Name("movable").BeginArray();
			foreach (Team t3 in world.UserTeams())
			{
				if (t3.VenueId != v.Id && world.CanHost(v, t3.Sport, t3.Level, -1, out var _))
				{
					j.BeginObject().Prop("id", t3.Id).Prop("label", world.TeamName(t3.Id) + " (" + SportRules.LevelLabel(t3.Sport, t3.Level) + " " + SportRules.SportLabel(t3.Sport) + ", " + SportsWorld.HomeDates(t3.Sport, t3.Level) + " home dates)")
						.EndObject();
				}
			}
			j.EndArray();
			j.Name("homeGames").BeginArray();
			foreach (Match g in UpcomingAtVenue(world, v.Id, 5, now))
			{
				Team home = world.GetTeam(g.HomeTeamId);
				League league = world.LeagueOf(home);
				bool tonight = g.Featured && GameClock.DayOf(g.Time) < GameClock.NextMonthStartDay(now);
				LiveSnapshot live = ((home == null || league == null) ? null : Live(home.Sport, league.Level, g, now));
				JsonBuilder jsonBuilder = j.BeginObject();
				string value;
				if (live != null)
				{
					value = "LIVE: " + world.TeamAbbrev(g.AwayTeamId) + " " + live.AwayScore + ", " + world.TeamAbbrev(g.HomeTeamId) + " " + live.HomeScore + " (" + live.Status + ")";
				}
				else
				{
					value = (tonight ? ("Tonight, " + GameClock.ClockLabel(GameClock.MinuteOfDay(g.Time))) : GameClock.TimeLabel(g.Time));
				}
				jsonBuilder.Prop("time", value).Prop("live", live != null).Prop("featured", tonight)
					.Prop("sport", (home == null) ? "" : SportRules.SportLabel(home.Sport))
					.Prop("label", world.TeamShortName(g.AwayTeamId) + " at " + world.TeamShortName(g.HomeTeamId))
					.Prop("stage", (league == null) ? "" : world.StageLabel(league, g))
					.EndObject();
			}
			j.EndArray();
			j.EndObject();
		}
		j.EndObject();
		return j.ToString();
	}

	private static void WriteDay(JsonBuilder j, SportsWorld world, League league, KeyValuePair<int, List<Match>> day)
	{
		j.BeginObject();
		j.Prop("label", GameClock.DayLabel(day.Key));
		j.Name("games").BeginArray();
		foreach (Match g in day.Value)
		{
			WriteGame(j, world, league, g);
		}
		j.EndArray();
		j.EndObject();
	}

	private static void WritePlayoffs(JsonBuilder j, SportsWorld world, League league, Season s)
	{
		j.Name("playoffs").BeginObject();
		j.Prop("title", world.SeasonLabel(league, s) + " playoffs");
		j.Prop("champion", (s.ChampionTeamId >= 0) ? (world.TeamName(s.ChampionTeamId) + " are the " + world.SeasonLabel(league, s) + " " + SportsWorld.Profile(league).ChampionLabel.ToLowerInvariant() + ".") : "");
		j.Name("rounds").BeginArray();
		for (int round = 0; round <= SportsWorld.CurrentRound(s); round++)
		{
			List<PlayoffSeries> series = SportsWorld.SeriesInRound(s, round);
			if (series.Count == 0)
			{
				continue;
			}
			PlayoffRoundFormat f = SportsWorld.FormatFor(SportsWorld.Profile(league), s, round);
			j.BeginObject();
			j.Prop("name", f.Name);
			string format = ((f.BestOf > 1) ? ("Best of " + f.BestOf) : "One game");
			if (f.Neutral)
			{
				format += ", neutral site";
			}
			j.Prop("format", format);
			j.Name("series").BeginArray();
			foreach (PlayoffSeries ps in series)
			{
				WriteSeries(j, world, s, ps);
			}
			j.EndArray();
			j.EndObject();
		}
		j.EndArray();
		j.EndObject();
	}

	private static void WriteSeries(JsonBuilder j, SportsWorld world, Season s, PlayoffSeries ps)
	{
		Team high = world.GetTeam(ps.HighTeamId);
		Team low = world.GetTeam(ps.LowTeamId);
		j.BeginObject();
		j.Prop("id", ps.Id);
		j.Prop("high", world.TeamName(ps.HighTeamId));
		j.Prop("highSeed", ps.HighSeed);
		j.Prop("low", ps.IsBye ? "" : world.TeamName(ps.LowTeamId));
		j.Prop("lowSeed", ps.LowSeed);
		j.Prop("isUser", (high != null && high.IsUser) || (low?.IsUser ?? false));
		j.Prop("decided", ps.WinnerId >= 0);
		j.Prop("highWon", ps.WinnerId >= 0 && ps.WinnerId == ps.HighTeamId && !ps.IsBye);
		j.Prop("lowWon", ps.WinnerId >= 0 && ps.WinnerId == ps.LowTeamId);
		j.Prop("status", SeriesStatus(world, s, ps));
		j.EndObject();
	}

	public static string SeriesStatus(SportsWorld world, Season s, PlayoffSeries ps)
	{
		if (ps.IsBye)
		{
			return "Bye to the next round";
		}
		SportsWorld.SeriesWins(s, ps, out var high, out var low);
		if (ps.BestOf <= 1)
		{
			Match game = null;
			foreach (Match g in s.Games)
			{
				if (g.SeriesId == ps.Id && g.Stage == GameStage.Playoff)
				{
					game = g;
				}
			}
			if (game == null)
			{
				return "";
			}
			if (!game.Played)
			{
				return GameClock.TimeLabel(game.Time) + ((game.VenueId == -2) ? ", neutral site" : "");
			}
			int w = game.WinnerId;
			string score = Math.Max(game.HomeScore, game.AwayScore) + "-" + Math.Min(game.HomeScore, game.AwayScore);
			if (game.Result == ResultType.Penalties)
			{
				score += " on penalties";
			}
			else if (game.Result != ResultType.Regulation)
			{
				score = score + " " + SportRules.ResultSuffix(Sport.Hockey, game.Result);
			}
			return world.TeamShortName(w) + " won " + score;
		}
		if (ps.WinnerId >= 0)
		{
			return world.TeamShortName(ps.WinnerId) + " won " + Math.Max(high, low) + "-" + Math.Min(high, low);
		}
		if (high == 0 && low == 0)
		{
			int first = int.MaxValue;
			foreach (Match g2 in s.Games)
			{
				if (g2.SeriesId == ps.Id && g2.Stage == GameStage.Playoff)
				{
					first = Math.Min(first, g2.Time);
				}
			}
			if (first != int.MaxValue)
			{
				return "Game 1: " + GameClock.TimeLabel(first);
			}
			return "";
		}
		if (high == low)
		{
			return "Series tied " + high + "-" + low;
		}
		return world.TeamShortName((high > low) ? ps.HighTeamId : ps.LowTeamId) + " leads " + Math.Max(high, low) + "-" + Math.Min(high, low);
	}

	private static void WriteGame(JsonBuilder j, SportsWorld world, League league, Match g)
	{
		Team home = world.GetTeam(g.HomeTeamId);
		Team away = world.GetTeam(g.AwayTeamId);
		j.BeginObject();
		j.Prop("id", g.Id);
		j.Prop("time", GameClock.TimeLabel(g.Time));
		j.Prop("hour", GameClock.ClockLabel(GameClock.MinuteOfDay(g.Time)));
		j.Prop("home", world.TeamName(g.HomeTeamId));
		j.Prop("away", world.TeamName(g.AwayTeamId));
		j.Prop("homeAbbr", world.TeamAbbrev(g.HomeTeamId));
		j.Prop("awayAbbr", world.TeamAbbrev(g.AwayTeamId));
		j.Prop("isUser", (home != null && home.IsUser) || (away?.IsUser ?? false));
		j.Prop("where", WhereLabel(world, g));
		j.Prop("stage", world.StageLabel(league, g));
		string weather = SportsWorld.WeatherText(league.Sport, g);
		if (weather.Length > 0)
		{
			j.Prop("weather", weather);
		}
		j.Prop("played", g.Played);
		j.Prop("featured", g.Featured);
		j.Prop("spotlight", g.Spotlight);
		j.Prop("stars", g.Summary ?? "");
		j.Prop("rival", RivalTagText(world.FindRivalry(league, g.HomeTeamId, g.AwayTeamId, create: false), game: true));
		LiveSnapshot live = (g.Played ? null : Live(league.Sport, league.Level, g, world.LastTime));
		if (live != null)
		{
			j.Prop("live", value: true);
			j.Prop("homeScore", live.HomeScore);
			j.Prop("awayScore", live.AwayScore);
			j.Prop("liveStatus", live.Status);
		}
		if (g.Played)
		{
			int winner = g.WinnerId;
			j.Prop("homeScore", g.HomeScore);
			j.Prop("awayScore", g.AwayScore);
			j.Prop("homeWon", winner == g.HomeTeamId);
			j.Prop("awayWon", winner == g.AwayTeamId);
			string suffix = SportRules.ResultSuffix(league.Sport, g.Result);
			if (g.Result == ResultType.Penalties)
			{
				suffix = "PENS " + g.HomePenalties + "-" + g.AwayPenalties;
			}
			j.Prop("suffix", suffix);
		}
		j.EndObject();
	}

	private static void WriteTeamSummary(JsonBuilder j, SportsWorld world, Team t, int now)
	{
		Organization o = world.GetOrg(t.OrgId);
		League league = world.LeagueOf(t);
		Season s = league?.Current;
		bool lastSeason = league != null && league.Previous != null && (s == null || !s.AnyPlayed) && league.Previous.Games.Exists((Match match) => match.Involves(t.Id) && match.Played);
		if (lastSeason)
		{
			s = league.Previous;
		}
		StandingRow row = null;
		int teamsInLeague = 0;
		if (s != null && league.TeamIds.Contains(t.Id))
		{
			List<StandingRow> list = Standings.Compute(league, s);
			teamsInLeague = list.Count;
			foreach (StandingRow r in list)
			{
				if (r.TeamId == t.Id)
				{
					row = r;
				}
			}
		}
		j.BeginObject();
		j.Prop("id", t.Id);
		j.Prop("orgId", t.OrgId);
		j.Prop("name", (o == null) ? "TBD" : o.FullName);
		j.Prop("school", (o == null) ? "" : o.Name);
		j.Prop("mascot", (o == null) ? "" : o.Mascot);
		j.Prop("sport", (int)t.Sport);
		j.Prop("sportLabel", SportRules.SportLabel(t.Sport));
		j.Prop("level", (int)t.Level);
		j.Prop("levelLabel", SportRules.LevelLabel(t.Sport, t.Level));
		j.Prop("leagueKey", League.MakeKey(t.Sport, t.Level));
		j.Prop("record", (row == null) ? "0-0" : Standings.RecordText(t.Sport, row));
		j.Prop("standing", (row == null || row.Played == 0) ? "" : ((lastSeason ? "Finished " : "") + Ordinal(row.Rank) + " of " + teamsInLeague + (lastSeason ? " last season" : "")));
		string status = "";
		if (world.IsPending(t))
		{
			status = "Joins next season.";
		}
		else if (league != null)
		{
			status = TeamStatus(world, league, t, now);
		}
		j.Prop("status", status);
		List<Player> roster = world.RosterOf(t.Id);
		j.Prop("overall", TeamOvr(t, roster));
		j.Prop("players", roster.Count);
		Match featured = world.GameOfTheMonth(t, now);
		string gom = "";
		if (featured != null)
		{
			bool home = featured.HomeTeamId == t.Id;
			string opponent = world.TeamShortName(home ? featured.AwayTeamId : featured.HomeTeamId);
			string stage = ((featured.Stage != GameStage.Regular && league != null) ? (" (" + world.StageLabel(league, featured) + ")") : "");
			LiveSnapshot live = ((featured.Played || league == null) ? null : Live(league.Sport, league.Level, featured, now));
			if (live != null)
			{
				int mine = (home ? live.HomeScore : live.AwayScore);
				int theirs = (home ? live.AwayScore : live.HomeScore);
				gom = "LIVE " + (home ? "vs " : "at ") + opponent + ": " + world.TeamShortName(t.Id) + " " + mine + ", " + opponent + " " + theirs + " (" + live.Status + ")" + stage;
				j.Prop("liveScore", (home ? "vs " : "at ") + opponent + "   " + world.TeamAbbrev(t.Id) + " " + mine + ", " + world.TeamAbbrev(home ? featured.AwayTeamId : featured.HomeTeamId) + " " + theirs);
				j.Prop("liveStatus", live.Status + stage);
			}
			else
			{
				gom = (featured.Played ? (LastGameText(world, t.Id, featured) + stage) : ("Tonight at " + GameClock.ClockLabel(GameClock.MinuteOfDay(featured.Time)) + (home ? " vs " : " at ") + opponent + stage + ((featured.VenueId == -2) ? ", neutral site" : "")));
			}
			j.Prop("gameOfMonthId", featured.Id);
			j.Prop("gameOfMonthPlaying", live != null);
		}
		j.Prop("gameOfMonth", gom);
		j.Prop("gameOfMonthLive", featured != null && !featured.Played);
		j.Prop("gameOfMonthStars", (featured != null && featured.Played) ? (featured.Summary ?? "") : "");
		Match last = null;
		Match next = null;
		int restOfMonth = 0;
		int nextMonth = GameClock.NextMonthStartDay(now);
		if (s != null)
		{
			foreach (Match g in s.Games)
			{
				if (!g.Involves(t.Id))
				{
					continue;
				}
				if (g.Played && (last == null || g.Time > last.Time))
				{
					last = g;
				}
				if (!g.Played && !g.Featured)
				{
					if (GameClock.DayOf(g.Time) < nextMonth)
					{
						restOfMonth++;
					}
					else if (next == null || g.Time < next.Time)
					{
						next = g;
					}
				}
			}
		}
		j.Prop("monthNote", (restOfMonth == 0) ? "" : (restOfMonth + ((restOfMonth == 1) ? " more game" : " more games") + " this month. Results come in when the month ends."));
		j.Prop("watchFirst", t.WatchFirst);
		j.Prop("spotlight", featured?.Spotlight ?? false);
		j.Prop("noLiveNote", (featured == null && restOfMonth > 0 && t.FeaturedMonth == GameClock.MonthIndex(now)) ? "No live game this month: its games this month don't fit around your other teams' games at its venue." : "");
		Team parentClub = world.ParentOf(t);
		Team farmClub = world.AffiliateOf(t);
		JsonBuilder jsonBuilder = j;
		string value;
		if (parentClub != null)
		{
			value = "Farm team of the " + world.TeamName(parentClub.Id);
		}
		else if (farmClub != null)
		{
			value = "Farm team: " + world.TeamName(farmClub.Id);
		}
		else
		{
			value = ((t.Level == Level.Minor && t.Sport != Sport.Soccer) ? "Independent (no pro club yet)" : "");
		}
		jsonBuilder.Prop("farmLine", value);
		j.Prop("last", (last == null || last == featured) ? "" : (GameClock.ShortDayLabel(GameClock.DayOf(last.Time)) + ": " + LastGameText(world, t.Id, last)));
		j.Prop("next", (next == null) ? "" : NextGameText(world, league, t.Id, next));
		WriteProSummary(j, world, t, league, now);
		j.EndObject();
	}

	public static string LeagueTitle(League league)
	{
		return SportRules.LevelLabel(league.Sport, league.Level) + " " + SportRules.SportLabel(league.Sport);
	}

	public static string LeagueStatus(SportsWorld world, League league, int now)
	{
		Season s = league.Current;
		if (s == null)
		{
			if (league.PendingTeamIds.Count > 0)
			{
				return "Next season is being set up.";
			}
			return "No teams in your city right now. Add one at a venue to start a new season.";
		}
		int total = 0;
		int played = 0;
		int first = int.MaxValue;
		int last = int.MinValue;
		foreach (Match g in s.Games)
		{
			if (g.Stage == GameStage.Regular)
			{
				total++;
				if (g.Played)
				{
					played++;
				}
				first = Math.Min(first, g.Time);
				last = Math.Max(last, g.Time);
			}
		}
		if (!s.PlayoffsCreated)
		{
			if (played == 0 && first != int.MaxValue)
			{
				string prefix = "";
				if (league.Previous != null && league.Previous.ChampionTeamId >= 0)
				{
					prefix = world.SeasonLabel(league, league.Previous) + " champions: " + world.TeamName(league.Previous.ChampionTeamId) + ". ";
				}
				return prefix + "The " + world.SeasonLabel(league, s) + " season starts " + GameClock.DayLabel(GameClock.DayOf(first)) + ".";
			}
			string end = ((last == int.MinValue) ? "" : (" Regular season ends " + GameClock.DayLabel(GameClock.DayOf(last)) + "."));
			return "Regular season: " + played + " of " + total + " games played." + end;
		}
		int round = SportsWorld.CurrentRound(s);
		if (round < 0)
		{
			return "Season over.";
		}
		string name = world.RoundName(league, s, round);
		int next = int.MaxValue;
		bool anyPlayed = false;
		foreach (Match g2 in s.Games)
		{
			if (g2.Stage == GameStage.Playoff && g2.PlayoffRound == round)
			{
				if (g2.Played)
				{
					anyPlayed = true;
				}
				else
				{
					next = Math.Min(next, g2.Time);
				}
			}
		}
		if (!anyPlayed && next != int.MaxValue)
		{
			return "Playoffs: " + name + ", starting " + GameClock.DayLabel(GameClock.DayOf(next)) + ".";
		}
		return "Playoffs: " + name + ".";
	}

	public static string TeamStatus(SportsWorld world, League league, Team t, int now)
	{
		Season s = league.Current;
		if (s == null || !s.PlayoffsCreated || !league.TeamIds.Contains(t.Id))
		{
			return LeagueStatus(world, league, now);
		}
		PlayoffSeries latest = null;
		foreach (PlayoffSeries ps in s.Bracket)
		{
			if (ps.Involves(t.Id) && (latest == null || ps.Round > latest.Round))
			{
				latest = ps;
			}
		}
		if (latest == null)
		{
			return "Missed the playoffs. " + LeagueStatus(world, league, now);
		}
		string name = world.RoundName(league, s, latest.Round);
		if (latest.WinnerId >= 0 && latest.WinnerId != t.Id)
		{
			return "Knocked out in the " + name + ". " + LeagueStatus(world, league, now);
		}
		if (latest.IsBye)
		{
			return "Seeded " + Ordinal(latest.HighSeed) + ", bye in the " + name + ".";
		}
		string status = SeriesStatus(world, s, latest);
		return "Playoffs, " + name + " (seeded " + Ordinal(latest.SeedOf(t.Id)) + "): " + status + ".";
	}

	private static string LastGameText(SportsWorld world, int teamId, Match g)
	{
		bool home = g.HomeTeamId == teamId;
		int mine = (home ? g.HomeScore : g.AwayScore);
		int theirs = (home ? g.AwayScore : g.HomeScore);
		int winner = g.WinnerId;
		string result;
		if (winner == teamId)
		{
			result = "W";
		}
		else
		{
			result = ((winner < 0) ? "T" : "L");
		}
		string text = result + " " + mine + "-" + theirs;
		if (g.Result == ResultType.Penalties)
		{
			text += " (pens)";
		}
		else if (g.Result != ResultType.Regulation)
		{
			text = text + " " + SportRules.ResultSuffix(Sport.Hockey, g.Result);
		}
		return text + (home ? " vs " : " at ") + world.TeamShortName(home ? g.AwayTeamId : g.HomeTeamId);
	}

	private static string NextGameText(SportsWorld world, League league, int teamId, Match g)
	{
		bool home = g.HomeTeamId == teamId;
		string text = GameClock.TimeLabel(g.Time) + (home ? " vs " : " at ") + world.TeamShortName(home ? g.AwayTeamId : g.HomeTeamId);
		if (g.Stage != GameStage.Regular && league != null)
		{
			text = text + " (" + world.StageLabel(league, g) + ")";
		}
		if (g.VenueId == -2)
		{
			text += ", neutral site";
		}
		return text;
	}

	private static string WhereLabel(SportsWorld world, Match g)
	{
		if (g.VenueId == -2)
		{
			return "Neutral site";
		}
		if (g.VenueId >= 0)
		{
			return "In your city";
		}
		return "";
	}

	public static string Ordinal(int n)
	{
		int mod100 = n % 100;
		string suffix = "th";
		if (mod100 < 11 || mod100 > 13)
		{
			switch (n % 10)
			{
			case 1:
				suffix = "st";
				break;
			case 2:
				suffix = "nd";
				break;
			case 3:
				suffix = "rd";
				break;
			}
		}
		return n + suffix;
	}

	private static string SportsList(VenueType type)
	{
		List<string> names = new List<string>();
		Sport[] array = SportRules.AllowedSports(type);
		foreach (Sport s in array)
		{
			names.Add(SportRules.SportLabel(s));
		}
		return string.Join(", ", names.ToArray());
	}

	public static string[] Columns(Sport sport)
	{
		return sport switch
		{
			Sport.Hockey => new string[8] { "GP", "W", "L", "OTL", "PTS", "GF", "GA", "STRK" }, 
			Sport.Soccer => new string[8] { "GP", "W", "D", "L", "PTS", "GF", "GA", "STRK" }, 
			Sport.Baseball => new string[7] { "GP", "W", "L", "PCT", "RS", "RA", "STRK" }, 
			_ => new string[7] { "GP", "W", "L", "PCT", "PF", "PA", "STRK" }, 
		};
	}

	public static string[] Cells(Sport sport, StandingRow r)
	{
		string pct = ((r.Played == 0) ? ".000" : r.WinPct.ToString(".000", CultureInfo.InvariantCulture));
		if (r.Played > 0 && r.WinPct >= 1.0)
		{
			pct = "1.000";
		}
		return sport switch
		{
			Sport.Hockey => new string[8]
			{
				S(r.Played),
				S(r.Wins),
				S(r.Losses),
				S(r.OvertimeLosses),
				S(r.Points),
				S(r.PointsFor),
				S(r.PointsAgainst),
				r.Streak
			}, 
			Sport.Soccer => new string[8]
			{
				S(r.Played),
				S(r.Wins),
				S(r.Ties),
				S(r.Losses),
				S(r.Points),
				S(r.PointsFor),
				S(r.PointsAgainst),
				r.Streak
			}, 
			_ => new string[7]
			{
				S(r.Played),
				S(r.Wins),
				S(r.Losses),
				pct,
				S(r.PointsFor),
				S(r.PointsAgainst),
				r.Streak
			}, 
		};
	}

	private static string S(int v)
	{
		return v.ToString(CultureInfo.InvariantCulture);
	}

	public static List<KeyValuePair<int, List<Match>>> RecentDays(League league, int dayCount)
	{
		List<Match> played = new List<Match>();
		Season[] array = new Season[2] { league.Current, league.Previous };
		foreach (Season s in array)
		{
			if (s == null)
			{
				continue;
			}
			foreach (Match g in s.Games)
			{
				if (g.Played && g.AwayTeamId >= 0 && g.HomeTeamId >= 0)
				{
					played.Add(g);
				}
			}
		}
		played.Sort((Match a, Match b) => (b.Time == a.Time) ? a.Id.CompareTo(b.Id) : b.Time.CompareTo(a.Time));
		List<KeyValuePair<int, List<Match>>> days = new List<KeyValuePair<int, List<Match>>>();
		foreach (Match g2 in played)
		{
			int day = GameClock.DayOf(g2.Time);
			if (days.Count == 0 || days[days.Count - 1].Key != day)
			{
				if (days.Count >= dayCount)
				{
					break;
				}
				days.Add(new KeyValuePair<int, List<Match>>(day, new List<Match>()));
			}
			days[days.Count - 1].Value.Add(g2);
		}
		foreach (KeyValuePair<int, List<Match>> item in days)
		{
			item.Value.Sort((Match a, Match b) => (a.Time == b.Time) ? a.Id.CompareTo(b.Id) : a.Time.CompareTo(b.Time));
		}
		return days;
	}

	public static List<KeyValuePair<int, List<Match>>> UpcomingDays(Season s, int dayCount, int maxGames)
	{
		List<Match> list = new List<Match>();
		foreach (Match g in s.Games)
		{
			if (!g.Played)
			{
				list.Add(g);
			}
		}
		list.Sort((Match a, Match b) => (a.Time == b.Time) ? a.Id.CompareTo(b.Id) : a.Time.CompareTo(b.Time));
		List<KeyValuePair<int, List<Match>>> days = new List<KeyValuePair<int, List<Match>>>();
		int count = 0;
		foreach (Match g2 in list)
		{
			int day = GameClock.DayOf(g2.Time);
			if (days.Count == 0 || days[days.Count - 1].Key != day)
			{
				if (days.Count >= dayCount || (days.Count > 0 && count >= maxGames))
				{
					break;
				}
				days.Add(new KeyValuePair<int, List<Match>>(day, new List<Match>()));
			}
			days[days.Count - 1].Value.Add(g2);
			count++;
		}
		return days;
	}

	public static List<Match> Upcoming(List<Match> games, int count)
	{
		List<Match> list = new List<Match>();
		foreach (Match g in games)
		{
			if (!g.Played)
			{
				list.Add(g);
			}
		}
		list.Sort((Match a, Match b) => (a.Time == b.Time) ? a.Id.CompareTo(b.Id) : a.Time.CompareTo(b.Time));
		if (list.Count > count)
		{
			list.RemoveRange(count, list.Count - count);
		}
		return list;
	}

	public static List<Match> UpcomingAtVenue(SportsWorld world, int venueId, int count, int now)
	{
		int nextMonth = GameClock.NextMonthStartDay(now);
		List<Match> tonight = new List<Match>();
		List<Match> later = new List<Match>();
		foreach (League l in world.Leagues)
		{
			if (l.Current == null)
			{
				continue;
			}
			foreach (Match g in l.Current.Games)
			{
				if (g.Played || g.VenueId != venueId)
				{
					continue;
				}
				if (GameClock.DayOf(g.Time) < nextMonth)
				{
					if (g.Featured)
					{
						tonight.Add(g);
					}
				}
				else
				{
					later.Add(g);
				}
			}
		}
		tonight.Sort((Match a, Match b) => GameClock.MinuteOfDay(a.Time).CompareTo(GameClock.MinuteOfDay(b.Time)));
		later.Sort((Match a, Match b) => (a.Time == b.Time) ? a.Id.CompareTo(b.Id) : a.Time.CompareTo(b.Time));
		List<Match> list = new List<Match>(tonight);
		list.AddRange(later);
		if (list.Count > count)
		{
			list.RemoveRange(count, list.Count - count);
		}
		return list;
	}

	private static string N(int v)
	{
		return v.ToString(CultureInfo.InvariantCulture);
	}

	private static int G(int[] s, int i)
	{
		return Stats.Get(s, i);
	}

	private static string Pct(int made, int tries)
	{
		if (tries > 0)
		{
			return ((double)made * 100.0 / (double)tries).ToString("0", CultureInfo.InvariantCulture) + "%";
		}
		return "-";
	}

	private static BoxGroup[] BoxGroups(Sport sport)
	{
		return sport switch
		{
			Sport.Hockey => new BoxGroup[2]
			{
				new BoxGroup
				{
					Title = "Skaters",
					Shows = (Player p, int[] s) =>
					{
						if (p != null && p.Position == Position.Goalie)
						{
							return false;
						}
						return G(s, 11) == 0 && G(s, 8) == 0;
					},
					Order = (int[] s) => (double)((G(s, 1) + G(s, 2)) * 10 + G(s, 1) * 2) + (double)G(s, 3) * 0.1,
					Columns = new BoxColumn[6]
					{
						new BoxColumn("G", (int[] s) => N(G(s, 1))),
						new BoxColumn("A", (int[] s) => N(G(s, 2))),
						new BoxColumn("PTS", (int[] s) => N(G(s, 1) + G(s, 2))),
						new BoxColumn("+/-", (int[] s) => Stats.PlusMinus(G(s, 12))),
						new BoxColumn("SOG", (int[] s) => N(G(s, 3))),
						new BoxColumn("PIM", (int[] s) => N(G(s, 4)))
					}
				},
				new BoxGroup
				{
					Title = "Goalies",
					Shows = (Player p, int[] s) => G(s, 11) > 0 || G(s, 8) > 0,
					Order = null,
					Columns = new BoxColumn[5]
					{
						new BoxColumn("SA", (int[] s) => N(G(s, 8))),
						new BoxColumn("GA", (int[] s) => N(G(s, 9))),
						new BoxColumn("SV", (int[] s) => N(G(s, 8) - G(s, 9))),
						new BoxColumn("SV%", (int[] s) => (G(s, 8) > 0) ? ((double)(G(s, 8) - G(s, 9)) / (double)G(s, 8)).ToString(".000", CultureInfo.InvariantCulture) : "-"),
						new BoxColumn("DEC", (int[] s) =>
						{
							string result = ((G(s, 5) <= 0) ? ((G(s, 6) <= 0) ? ((G(s, 7) <= 0) ? "" : "OTL") : "L") : "W");
							return result;
						})
					}
				}
			}, 
			Sport.Basketball => new BoxGroup[1]
			{
				new BoxGroup
				{
					Title = "Players",
					Shows = (Player p, int[] s) => G(s, 11) > 0,
					Order = (int[] s) => G(s, 12) * 1000 + G(s, 11),
					Columns = new BoxColumn[10]
					{
						new BoxColumn("MIN", (int[] s) => N(G(s, 11))),
						new BoxColumn("PTS", (int[] s) => N(G(s, 1))),
						new BoxColumn("REB", (int[] s) => N(G(s, 2))),
						new BoxColumn("AST", (int[] s) => N(G(s, 3))),
						new BoxColumn("STL", (int[] s) => N(G(s, 4))),
						new BoxColumn("BLK", (int[] s) => N(G(s, 5))),
						new BoxColumn("FG", (int[] s) => G(s, 6) + "-" + G(s, 7)),
						new BoxColumn("3PM", (int[] s) => N(G(s, 8))),
						new BoxColumn("FT", (int[] s) => G(s, 9) + "-" + G(s, 10)),
						new BoxColumn("+/-", (int[] s) => Stats.PlusMinus(G(s, 13)))
					}
				}
			}, 
			Sport.Football => new BoxGroup[5]
			{
				new BoxGroup
				{
					Title = "Passing",
					Shows = (Player p, int[] s) => G(s, 2) > 0,
					Order = (int[] s) => G(s, 3),
					Columns = new BoxColumn[4]
					{
						new BoxColumn("C/ATT", (int[] s) => G(s, 1) + "/" + G(s, 2)),
						new BoxColumn("YDS", (int[] s) => N(G(s, 3))),
						new BoxColumn("TD", (int[] s) => N(G(s, 4))),
						new BoxColumn("INT", (int[] s) => N(G(s, 5)))
					}
				},
				new BoxGroup
				{
					Title = "Rushing",
					Shows = (Player p, int[] s) => G(s, 6) > 0,
					Order = (int[] s) => G(s, 7),
					Columns = new BoxColumn[4]
					{
						new BoxColumn("CAR", (int[] s) => N(G(s, 6))),
						new BoxColumn("YDS", (int[] s) => N(G(s, 7))),
						new BoxColumn("AVG", (int[] s) => (G(s, 6) > 0) ? ((double)G(s, 7) / (double)G(s, 6)).ToString("0.0", CultureInfo.InvariantCulture) : "-"),
						new BoxColumn("TD", (int[] s) => N(G(s, 8)))
					}
				},
				new BoxGroup
				{
					Title = "Receiving",
					Shows = (Player p, int[] s) => G(s, 9) > 0,
					Order = (int[] s) => G(s, 10),
					Columns = new BoxColumn[3]
					{
						new BoxColumn("REC", (int[] s) => N(G(s, 9))),
						new BoxColumn("YDS", (int[] s) => N(G(s, 10))),
						new BoxColumn("TD", (int[] s) => N(G(s, 11)))
					}
				},
				new BoxGroup
				{
					Title = "Defense",
					Shows = (Player p, int[] s) => G(s, 12) + G(s, 13) + G(s, 14) > 0,
					Order = (int[] s) => G(s, 12) + G(s, 13) * 3 + G(s, 14) * 4,
					Columns = new BoxColumn[3]
					{
						new BoxColumn("TCK", (int[] s) => N(G(s, 12))),
						new BoxColumn("SACK", (int[] s) => N(G(s, 13))),
						new BoxColumn("INT", (int[] s) => N(G(s, 14)))
					}
				},
				new BoxGroup
				{
					Title = "Kicking",
					Shows = (Player p, int[] s) => G(s, 16) + G(s, 18) > 0,
					Order = null,
					Columns = new BoxColumn[2]
					{
						new BoxColumn("FG", (int[] s) => G(s, 15) + "/" + G(s, 16)),
						new BoxColumn("XP", (int[] s) => G(s, 17) + "/" + G(s, 18))
					}
				}
			}, 
			Sport.Baseball => new BoxGroup[2]
			{
				new BoxGroup
				{
					Title = "Batting",
					Shows = (Player p, int[] s) => G(s, 1) + G(s, 7) > 0,
					Order = null,
					Columns = new BoxColumn[7]
					{
						new BoxColumn("AB", (int[] s) => N(G(s, 1))),
						new BoxColumn("R", (int[] s) => N(G(s, 2))),
						new BoxColumn("H", (int[] s) => N(G(s, 3))),
						new BoxColumn("RBI", (int[] s) => N(G(s, 6))),
						new BoxColumn("HR", (int[] s) => N(G(s, 5))),
						new BoxColumn("BB", (int[] s) => N(G(s, 7))),
						new BoxColumn("SO", (int[] s) => N(G(s, 8)))
					}
				},
				new BoxGroup
				{
					Title = "Pitching",
					Shows = (Player p, int[] s) => G(s, 9) > 0,
					Order = (int[] s) => G(s, 10) * 1000 + G(s, 11),
					Columns = new BoxColumn[6]
					{
						new BoxColumn("IP", (int[] s) => Stats.Innings(G(s, 11))),
						new BoxColumn("H", (int[] s) => N(G(s, 12))),
						new BoxColumn("ER", (int[] s) => N(G(s, 13))),
						new BoxColumn("BB", (int[] s) => N(G(s, 14))),
						new BoxColumn("K", (int[] s) => N(G(s, 15))),
						new BoxColumn("DEC", (int[] s) => (G(s, 16) <= 0) ? ((G(s, 17) <= 0) ? ((G(s, 18) <= 0) ? "" : "SV") : "L") : "W")
					}
				}
			}, 
			_ => new BoxGroup[2]
			{
				new BoxGroup
				{
					Title = "Players",
					Shows = (Player p, int[] s) => G(s, 8) == 0 && G(s, 5) == 0 && G(s, 6) == 0,
					Order = (int[] s) => G(s, 1) * 10 + G(s, 2) * 6 + G(s, 3),
					Columns = new BoxColumn[5]
					{
						new BoxColumn("G", (int[] s) => N(G(s, 1))),
						new BoxColumn("A", (int[] s) => N(G(s, 2))),
						new BoxColumn("SH", (int[] s) => N(G(s, 3))),
						new BoxColumn("YC", (int[] s) => N(G(s, 4))),
						new BoxColumn("RC", (int[] s) => N(G(s, 9)))
					}
				},
				new BoxGroup
				{
					Title = "Goalkeepers",
					Shows = (Player p, int[] s) => G(s, 8) > 0 || G(s, 5) > 0 || G(s, 6) > 0,
					Order = null,
					Columns = new BoxColumn[2]
					{
						new BoxColumn("SV", (int[] s) => N(G(s, 5))),
						new BoxColumn("GA", (int[] s) => N(G(s, 6)))
					}
				}
			}, 
		};
	}

	public static string FieldPosition(int yard, string offense, string defense)
	{
		if (yard == 50)
		{
			return "50";
		}
		if (yard >= 50)
		{
			return defense + " " + (100 - yard);
		}
		return offense + " " + yard;
	}

	private static string DriveResult(int result)
	{
		return result switch
		{
			0 => "Touchdown", 
			1 => "Field goal", 
			2 => "Missed field goal", 
			3 => "Punt", 
			4 => "Interception", 
			5 => "Fumble", 
			6 => "Turnover on downs", 
			7 => "End of half", 
			8 => "Safety", 
			9 => "End of game", 
			_ => "Driving", 
		};
	}

	private static void WriteDrives(JsonBuilder j, List<int> flow, string awayAbbr, string homeAbbr)
	{
		int n = 8;
		j.Name("drives").BeginArray();
		int prevSide = -1;
		int prevResult = -1;
		for (int i = 0; i + n <= flow.Count; i += n)
		{
			bool home = flow[i] == 1;
			int period = flow[i + 1];
			int start = flow[i + 2];
			int end = flow[i + 3];
			int plays = flow[i + 4];
			int yards = flow[i + 5];
			int result = flow[i + 6];
			int from = flow[i + 7];
			string off = (home ? homeAbbr : awayAbbr);
			string def = (home ? awayAbbr : homeAbbr);
			int into = ((period <= 4) ? (start - (period - 1) * 900) : (start - 3600 - (period - 5) * 600));
			int left = Math.Max(0, ((period <= 4) ? 900 : 600) - into);
			bool onside = prevSide == (home ? 1 : 0) && (prevResult == 0 || prevResult == 1);
			JsonBuilder jsonBuilder = j.BeginObject();
			string value;
			if (period <= 4)
			{
				value = "Q" + period;
			}
			else
			{
				value = ((period == 5) ? "OT" : (period - 4 + "OT"));
			}
			jsonBuilder.Prop("q", value).Prop("clock", Timeline.MinSec(left)).Prop("team", off)
				.Prop("home", home)
				.Prop("from", FieldPosition(Math.Max(1, Math.Min(99, from)), off, def) + (onside ? " (onside kick)" : ""))
				.Prop("plays", plays + ((plays == 1) ? " play, " : " plays, ") + yards + ((yards == 1 || yards == -1) ? " yd" : " yds"))
				.Prop("time", Timeline.MinSec(Math.Max(0, end - start)))
				.Prop("result", DriveResult(result))
				.Prop("scored", result == 0 || result == 1)
				.EndObject();
			prevSide = (home ? 1 : 0);
			prevResult = result;
		}
		j.EndArray();
	}

	private static List<string[]> TeamStats(Sport sport, GameBox b)
	{
		int[] a = new int[Stats.Length(sport)];
		int[] h = new int[Stats.Length(sport)];
		foreach (BoxLine l in b.Lines)
		{
			Stats.AddInto(l.Home ? h : a, Stats.Fit(l.Stats, sport));
		}
		List<string[]> rows = new List<string[]>();
		Action<string, string, string> add = (string label, string x, string y) =>
		{
			rows.Add(new string[3] { label, x, y });
		};
		switch (sport)
		{
		case Sport.Hockey:
		{
			add("Shots on goal", N(G(a, 3)), N(G(h, 3)));
			int ppa = 0;
			int pph = 0;
			foreach (BoxEvent e2 in b.Events)
			{
				if (e2.Kind == 0 && e2.Text.EndsWith("  PP"))
				{
					if (e2.Home)
					{
						pph++;
					}
					else
					{
						ppa++;
					}
				}
			}
			add("Power play goals", N(ppa), N(pph));
			add("Penalty minutes", N(G(a, 4)), N(G(h, 4)));
			add("Saves", N(G(a, 8) - G(a, 9)), N(G(h, 8) - G(h, 9)));
			break;
		}
		case Sport.Basketball:
			add("Field goals", G(a, 6) + "-" + G(a, 7) + " (" + Pct(G(a, 6), G(a, 7)) + ")", G(h, 6) + "-" + G(h, 7) + " (" + Pct(G(h, 6), G(h, 7)) + ")");
			add("Three pointers", N(G(a, 8)), N(G(h, 8)));
			add("Free throws", G(a, 9) + "-" + G(a, 10), G(h, 9) + "-" + G(h, 10));
			add("Rebounds", N(G(a, 2)), N(G(h, 2)));
			add("Assists", N(G(a, 3)), N(G(h, 3)));
			add("Steals", N(G(a, 4)), N(G(h, 4)));
			add("Blocks", N(G(a, 5)), N(G(h, 5)));
			break;
		case Sport.Football:
		{
			add("Total yards", N(G(a, 3) + G(a, 7)), N(G(h, 3) + G(h, 7)));
			add("Passing yards", N(G(a, 3)), N(G(h, 3)));
			add("Rushing yards", N(G(a, 7)), N(G(h, 7)));
			add("Interceptions thrown", N(G(a, 5)), N(G(h, 5)));
			add("Sacks", N(G(a, 13)), N(G(h, 13)));
			if (b.Flow.Count < 8)
			{
				break;
			}
			int[] top = new int[2];
			int[] turnovers = new int[2];
			int[] punts = new int[2];
			int[] drives = new int[2];
			for (int i = 0; i + 8 <= b.Flow.Count; i += 8)
			{
				int side = ((b.Flow[i] == 1) ? 1 : 0);
				int result = b.Flow[i + 6];
				drives[side]++;
				if (b.Flow[i + 1] <= 4)
				{
					top[side] += Math.Max(0, b.Flow[i + 3] - b.Flow[i + 2]);
				}
				if (result == 4 || result == 5)
				{
					turnovers[side]++;
				}
				if (result == 3)
				{
					punts[side]++;
				}
			}
			add("Turnovers", N(turnovers[0]), N(turnovers[1]));
			add("Punts", N(punts[0]), N(punts[1]));
			add("Drives", N(drives[0]), N(drives[1]));
			add("Time of possession", Timeline.MinSec(top[0]), Timeline.MinSec(top[1]));
			break;
		}
		case Sport.Baseball:
		{
			int ea = 0;
			int eh = 0;
			foreach (BoxEvent e in b.Events)
			{
				if (e.Text.EndsWith("on an error"))
				{
					if (e.Home)
					{
						ea++;
					}
					else
					{
						eh++;
					}
				}
			}
			add("Hits", N(G(a, 3)), N(G(h, 3)));
			add("Errors", N(ea), N(eh));
			add("Home runs", N(G(a, 5)), N(G(h, 5)));
			add("Walks", N(G(a, 7)), N(G(h, 7)));
			add("Strikeouts", N(G(a, 8)), N(G(h, 8)));
			break;
		}
		default:
			add("Shots", N(G(a, 3)), N(G(h, 3)));
			add("Saves", N(G(a, 5)), N(G(h, 5)));
			add("Yellow cards", N(G(a, 4)), N(G(h, 4)));
			add("Red cards", N(G(a, 9)), N(G(h, 9)));
			break;
		}
		return rows;
	}

	private static void WriteSide(JsonBuilder j, string name, SportsWorld world, Team t, League league, Season s)
	{
		string record = "";
		if (t != null && s != null && league.TeamIds.Contains(t.Id))
		{
			foreach (StandingRow r in Standings.Compute(league, s))
			{
				if (r.TeamId == t.Id)
				{
					record = Standings.RecordText(t.Sport, r);
				}
			}
		}
		j.Name(name).BeginObject().Prop("id", t?.Id ?? (-1))
			.Prop("name", (t == null) ? "TBD" : world.TeamName(t.Id))
			.Prop("abbr", (t == null) ? "" : world.TeamAbbrev(t.Id))
			.Prop("record", record)
			.Prop("isUser", t?.IsUser ?? false)
			.EndObject();
	}

	private static void WriteGameView(JsonBuilder j, SportsWorld world, League league, Season s, Match g, int now)
	{
		Team home = world.GetTeam(g.HomeTeamId);
		Team away = world.GetTeam(g.AwayTeamId);
		Sport sport = league.Sport;
		j.Name("gameView").BeginObject();
		j.Prop("id", g.Id);
		j.Prop("leagueKey", league.Key);
		j.Prop("leagueTitle", LeagueTitle(league));
		j.Prop("sport", SportRules.SportLabel(sport));
		WriteSide(j, "home", world, home, league, s);
		WriteSide(j, "away", world, away, league, s);
		j.Prop("played", g.Played);
		j.Prop("when", GameClock.TimeLabel(g.Time));
		j.Prop("where", WhereLabel(world, g));
		j.Prop("stage", world.StageLabel(league, g));
		j.Prop("weather", SportsWorld.WeatherText(sport, g));
		j.Prop("attendance", (g.Played || g.Live != null) ? world.AttendanceText(league, g) : "");
		if (world.FanNotes.TryGetValue(g.Id, out var fans))
		{
			j.Prop("fans", fans);
		}
		else if (g.Spotlight && world.FanStatus.Length > 0)
		{
			j.Prop("fans", world.FanStatus);
		}
		j.Prop("featured", g.Featured);
		j.Prop("spotlight", g.Spotlight);
		j.Prop("rival", RivalTagText(world.FindRivalry(league, g.HomeTeamId, g.AwayTeamId, create: false), game: true));
		PlayoffSeries ps = ((g.Stage == GameStage.Playoff) ? s.FindSeries(g.SeriesId) : null);
		j.Prop("series", (ps == null || ps.BestOf <= 1) ? "" : SeriesText(world, s, ps, g));
		LiveSnapshot live = (g.Played ? null : Live(sport, league.Level, g, now));
		if (g.Played)
		{
			j.Prop("homeScore", g.HomeScore);
			j.Prop("awayScore", g.AwayScore);
			JsonBuilder jsonBuilder = j;
			string value;
			if (g.WinnerId == g.HomeTeamId)
			{
				value = "home";
			}
			else
			{
				value = ((g.WinnerId == g.AwayTeamId) ? "away" : "");
			}
			jsonBuilder.Prop("winner", value);
			string suffix = SportRules.ResultSuffix(sport, g.Result);
			j.Prop("status", "Final" + ((suffix.Length > 0) ? ("/" + suffix) : "") + ((g.Result == ResultType.Penalties) ? (" (penalties " + g.HomePenalties + "-" + g.AwayPenalties + ")") : ""));
			j.Prop("stars", g.Summary ?? "");
			if (g.Box != null)
			{
				WriteBox(j, world, league, g.Box, g.HomeScore, g.AwayScore, g.WinnerId, g, home, away);
			}
			else
			{
				j.Prop("boxNote", "The box score for this game wasn't kept. Box scores are kept for your teams' games this season and everyone's games of the last " + 7 + " days.");
			}
		}
		else if (live != null)
		{
			j.Prop("live", value: true);
			j.Prop("progress", (int)Math.Round(live.Progress * 100.0));
			j.Prop("homeScore", live.HomeScore);
			j.Prop("awayScore", live.AwayScore);
			j.Prop("winner", "");
			j.Prop("status", (live.Progress >= 1.0) ? "Final, wrapping up" : live.Status);
			if (sport == Sport.Football && live.DriveHome >= 0 && live.Progress < 1.0)
			{
				string off = world.TeamAbbrev((live.DriveHome == 1) ? g.HomeTeamId : g.AwayTeamId);
				string def = world.TeamAbbrev((live.DriveHome == 1) ? g.AwayTeamId : g.HomeTeamId);
				j.Prop("situation", off + " ball at the " + FieldPosition(live.DriveYard, off, def) + ". This drive: " + live.DrivePlays + ((live.DrivePlays == 1) ? " play, " : " plays, ") + live.DriveYards + ((live.DriveYards == 1 || live.DriveYards == -1) ? " yard." : " yards."));
			}
			j.Prop("liveNote", "Live on your city's clock. Ends around " + GameClock.ClockLabel(GameClock.MinuteOfDay(g.Live.End)) + ". The score and the box score fill in as it goes; the result counts in the standings when it's over.");
			WriteBox(j, world, league, live.Box, live.HomeScore, live.AwayScore, -1, g, home, away);
		}
		else
		{
			j.Prop("status", GameClock.TimeLabel(g.Time));
			WritePreview(j, world, league, s, g, home, away);
		}
		j.EndObject();
	}

	private static string SeriesText(SportsWorld world, Season s, PlayoffSeries ps, Match current)
	{
		int high = 0;
		int low = 0;
		foreach (Match g in s.Games)
		{
			if (g.SeriesId == ps.Id && g.Played && g.Time <= current.Time && g != current)
			{
				if (g.WinnerId == ps.HighTeamId)
				{
					high++;
				}
				else if (g.WinnerId == ps.LowTeamId)
				{
					low++;
				}
			}
		}
		string label = "Best of " + ps.BestOf + ": ";
		if (high == low)
		{
			return label + "series tied " + high + "-" + low;
		}
		return label + world.TeamShortName((high > low) ? ps.HighTeamId : ps.LowTeamId) + " lead " + Math.Max(high, low) + "-" + Math.Min(high, low);
	}

	private static void WriteBox(JsonBuilder j, SportsWorld world, League league, GameBox b, int homeScore, int awayScore, int winnerId, Match g, Team home, Team away)
	{
		Sport sport = league.Sport;
		int regulation = Timeline.Regulation(sport);
		bool shootout = sport == Sport.Hockey && (b.Note.StartsWith("Shootout") || (!g.Played && g.Live != null && g.Live.Result == ResultType.Shootout));
		int periods = Math.Max(b.HomePeriods.Count, b.AwayPeriods.Count);
		j.Name("lineScore").BeginObject();
		j.Name("labels").BeginArray();
		for (int i = 0; i < periods; i++)
		{
			j.Value((shootout && i == periods - 1) ? "SO" : Timeline.PeriodLabel(sport, i + 1, regulation));
		}
		j.Value((sport == Sport.Baseball) ? "R" : "T");
		j.EndArray();
		Action<string, List<int>, int> action = (string name, List<int> list2, int total) =>
		{
			j.Name(name).BeginArray();
			for (int k = 0; k < periods; k++)
			{
				JsonBuilder jsonBuilder4 = j;
				string value4 = ((k < list2.Count) ? ((list2[k] == -1) ? "x" : ((list2[k] < 0) ? "" : N(list2[k]))) : "");
				jsonBuilder4.Value(value4);
			}
			j.Value(N(total));
			j.EndArray();
		};
		action("away", b.AwayPeriods, awayScore);
		action("home", b.HomePeriods, homeScore);
		j.EndObject();
		if (b.Note.Length > 0)
		{
			string winner = world.TeamShortName(winnerId);
			JsonBuilder jsonBuilder = j;
			string value;
			if (b.Note.StartsWith("Shootout"))
			{
				value = "Shootout: " + winner + " win it " + b.Note.Substring(9);
			}
			else
			{
				value = (b.Note.StartsWith("Penalties") ? ("Penalties: " + winner + " win " + b.Note.Substring(10)) : b.Note);
			}
			jsonBuilder.Prop("note", value);
		}
		string awayAbbr = world.TeamAbbrev(g.AwayTeamId);
		string homeAbbr = world.TeamAbbrev(g.HomeTeamId);
		j.Name("summary").BeginArray();
		int open = -1;
		foreach (BoxEvent e in b.Events)
		{
			int group = ((sport == Sport.Baseball) ? ((e.Period + 1) / 2) : e.Period);
			if (group != open)
			{
				if (open >= 0)
				{
					j.EndArray().EndObject();
				}
				open = group;
				j.BeginObject().Prop("title", Timeline.PeriodHeading(sport, e.Period, regulation));
				j.Name("events").BeginArray();
			}
			JsonBuilder jsonBuilder2 = j.BeginObject().Prop("clock", e.Clock).Prop("team", e.Home ? homeAbbr : awayAbbr)
				.Prop("home", e.Home)
				.Prop("text", e.Text);
			string value2;
			if (e.Kind != 0)
			{
				value2 = "";
			}
			else
			{
				value2 = ((sport == Sport.Baseball || sport == Sport.Football || sport == Sport.Basketball) ? (awayAbbr + " " + e.AwayScore + ", " + homeAbbr + " " + e.HomeScore) : (e.AwayScore + "-" + e.HomeScore));
			}
			jsonBuilder2.Prop("score", value2).Prop("kind", e.Kind).EndObject();
		}
		if (open >= 0)
		{
			j.EndArray().EndObject();
		}
		j.EndArray();
		JsonBuilder jsonBuilder3 = j;
		string value3;
		if (sport == Sport.Basketball)
		{
			value3 = "Points by quarter are in the line score. Top performers are in the box score below.";
		}
		else if (b.Events.Count != 0)
		{
			value3 = "";
		}
		else
		{
			value3 = (g.Played ? "No scoring." : "No scoring yet.");
		}
		jsonBuilder3.Prop("summaryNote", value3);
		if (sport == Sport.Football && b.Flow.Count >= 8)
		{
			WriteDrives(j, b.Flow, awayAbbr, homeAbbr);
		}
		j.Name("teamStats").BeginArray();
		foreach (string[] r in TeamStats(sport, b))
		{
			j.BeginObject().Prop("label", r[0]).Prop("away", r[1])
				.Prop("home", r[2])
				.EndObject();
		}
		j.EndArray();
		j.Name("boxes").BeginArray();
		bool[] array = new bool[2] { false, true };
		foreach (bool isHome in array)
		{
			Team t = (isHome ? home : away);
			j.BeginObject().Prop("team", (t == null) ? "" : world.TeamName(t.Id)).Prop("teamId", t?.Id ?? (-1));
			j.Name("groups").BeginArray();
			BoxGroup[] array2 = BoxGroups(sport);
			foreach (BoxGroup grp in array2)
			{
				List<KeyValuePair<BoxLine, Player>> lines = new List<KeyValuePair<BoxLine, Player>>();
				foreach (BoxLine l in b.Lines)
				{
					if (l.Home == isHome)
					{
						Player p = world.AnyPlayer(l.PlayerId);
						int[] st = Stats.Fit(l.Stats, sport);
						if (grp.Shows(p, st))
						{
							lines.Add(new KeyValuePair<BoxLine, Player>(l, p));
						}
					}
				}
				if (lines.Count == 0)
				{
					continue;
				}
				if (grp.Order != null)
				{
					List<KeyValuePair<BoxLine, Player>> list = new List<KeyValuePair<BoxLine, Player>>(lines);
					list.Sort((KeyValuePair<BoxLine, Player> x, KeyValuePair<BoxLine, Player> y) => grp.Order(Stats.Fit(y.Key.Stats, sport)).CompareTo(grp.Order(Stats.Fit(x.Key.Stats, sport))));
					lines = list;
				}
				j.BeginObject().Prop("title", grp.Title);
				j.Name("columns").BeginArray();
				BoxColumn[] columns = grp.Columns;
				foreach (BoxColumn c in columns)
				{
					j.Value(c.Title);
				}
				j.EndArray();
				j.Name("rows").BeginArray();
				foreach (KeyValuePair<BoxLine, Player> kv in lines)
				{
					int[] st2 = Stats.Fit(kv.Key.Stats, sport);
					Player p2 = kv.Value;
					j.BeginObject().Prop("id", p2?.Id ?? (-1)).Prop("name", (p2 == null) ? "Former player" : p2.Name)
						.Prop("pos", (p2 == null) ? "" : Rosters.PositionLabel(p2.Position));
					j.Name("cells").BeginArray();
					columns = grp.Columns;
					foreach (BoxColumn c2 in columns)
					{
						j.Value(c2.Cell(st2));
					}
					j.EndArray();
					j.EndObject();
				}
				j.EndArray();
				j.EndObject();
			}
			j.EndArray();
			j.EndObject();
		}
		j.EndArray();
	}

	private static void WritePreview(JsonBuilder j, SportsWorld world, League league, Season s, Match g, Team home, Team away)
	{
		Sport sport = league.Sport;
		int homePct = (int)Math.Round(((home != null && away != null) ? world.HomeChance(league, s, g) : 0.5) * 100.0);
		j.Name("preview").BeginObject();
		j.Prop("homeChance", homePct);
		j.Prop("awayChance", 100 - homePct);
		j.Prop("drawNote", (sport == Sport.Soccer && g.Stage == GameStage.Regular) ? "Soccer games can end in a draw; the chances are each team's share of a decided result." : "");
		j.Name("headToHead").BeginArray();
		foreach (Match x in s.Games)
		{
			if (x.Played && x != g && x.Involves(g.HomeTeamId) && x.Involves(g.AwayTeamId))
			{
				j.BeginObject().Prop("id", x.Id).Prop("text", GameClock.ShortDayLabel(GameClock.DayOf(x.Time)) + ": " + world.TeamAbbrev(x.AwayTeamId) + " " + x.AwayScore + ", " + world.TeamAbbrev(x.HomeTeamId) + " " + x.HomeScore + ((SportRules.ResultSuffix(sport, x.Result).Length > 0) ? (" (" + SportRules.ResultSuffix(sport, x.Result) + ")") : ""))
					.EndObject();
			}
		}
		j.EndArray();
		Rivalry rv = world.FindRivalry(league, g.HomeTeamId, g.AwayTeamId, create: false);
		if (rv != null && rv.Heat >= 50)
		{
			int homeWins = ((rv.TeamA == g.HomeTeamId) ? rv.WinsA : rv.WinsB);
			int awayWins = ((rv.TeamA == g.HomeTeamId) ? rv.WinsB : rv.WinsA);
			j.Prop("rivalry", SportsWorld.RivalryLabel(rv) + ": all time " + world.TeamShortName(g.HomeTeamId) + " " + homeWins + ", " + world.TeamShortName(g.AwayTeamId) + " " + awayWins + ((rv.Ties > 0) ? (", " + rv.Ties + " draws") : ""));
		}
		j.Name("teams").BeginArray();
		Team[] array = new Team[2] { away, home };
		foreach (Team t in array)
		{
			j.BeginObject();
			j.Prop("name", (t == null) ? "TBD" : world.TeamName(t.Id));
			j.Prop("teamId", t?.Id ?? (-1));
			if (t != null)
			{
				j.Prop("form", FormText(world, s, t));
				j.Prop("starter", ProbableStarter(world, t, g));
				j.Prop("coach", (world.CoachOf(t.Id, CoachRole.Head) == null) ? "" : world.CoachOf(t.Id, CoachRole.Head).Name);
				j.Name("leaders").BeginArray();
				foreach (KeyValuePair<Player, string> kv in TeamLeaders(world, t))
				{
					j.BeginObject().Prop("id", kv.Key.Id).Prop("name", kv.Key.Name)
						.Prop("pos", Rosters.PositionLabel(kv.Key.Position))
						.Prop("text", kv.Value)
						.EndObject();
				}
				j.EndArray();
				j.Name("injuries").BeginArray();
				foreach (Player p in world.RosterOf(t.Id))
				{
					string hurt = world.InjuryText(p);
					if (hurt.Length > 0)
					{
						j.BeginObject().Prop("id", p.Id).Prop("name", p.Name)
							.Prop("pos", Rosters.PositionLabel(p.Position))
							.Prop("text", hurt)
							.EndObject();
					}
				}
				j.EndArray();
			}
			j.EndObject();
		}
		j.EndArray();
		j.EndObject();
	}

	private static string FormText(SportsWorld world, Season s, Team t)
	{
		List<Match> played = new List<Match>();
		foreach (Match x in s.Games)
		{
			if (x.Played && x.Involves(t.Id))
			{
				played.Add(x);
			}
		}
		played.Sort((Match a, Match b) => b.Time.CompareTo(a.Time));
		List<string> parts = new List<string>();
		for (int i = 0; i < played.Count && i < 5; i++)
		{
			int w = played[i].WinnerId;
			List<string> list = parts;
			string item;
			if (w == t.Id)
			{
				item = "W";
			}
			else if (w < 0)
			{
				item = "D";
			}
			else
			{
				item = ((t.Sport == Sport.Hockey && (played[i].Result == ResultType.Overtime || played[i].Result == ResultType.Shootout)) ? "OTL" : "L");
			}
			list.Add(item);
		}
		if (parts.Count != 0)
		{
			return "Last " + parts.Count + ": " + string.Join("-", parts.ToArray());
		}
		return "No games yet";
	}

	private static string ProbableStarter(SportsWorld world, Team t, Match g)
	{
		int day = Math.Max(world.Today(), GameClock.DayOf(g.Time));
		List<Player> roster = world.Healthy(t.Id, day);
		Func<Position, List<Player>> byPos = (Position pos) =>
		{
			List<Player> list = roster.FindAll((Player player) => player.Position == pos);
			list.Sort((Player a, Player b) => (b.Overall == a.Overall) ? a.Id.CompareTo(b.Id) : b.Overall.CompareTo(a.Overall));
			return list;
		};
		switch (t.Sport)
		{
		case Sport.Hockey:
		{
			List<Player> goalies = byPos(Position.Goalie);
			if (goalies.Count == 0)
			{
				return "";
			}
			Player p2 = goalies[0];
			int[] st2 = Stats.Fit(p2.Season, t.Sport);
			return "Probable goalie: " + p2.Name + ((G(st2, 11) > 0) ? (" (" + G(st2, 5) + "-" + G(st2, 6) + "-" + G(st2, 7) + ", " + ((G(st2, 8) <= 0) ? ".000" : ((double)(G(st2, 8) - G(st2, 9)) / (double)G(st2, 8)).ToString(".000", CultureInfo.InvariantCulture)) + ")") : "");
		}
		case Sport.Baseball:
		{
			List<Player> starters = roster.FindAll((Player x) => x.Position == Position.StartingPitcher);
			if (starters.Count == 0)
			{
				return "";
			}
			starters.Sort((Player a, Player b) => a.Id.CompareTo(b.Id));
			Player p = starters[t.Rotation % starters.Count];
			int[] st = Stats.Fit(p.Season, t.Sport);
			return "Probable pitcher: " + p.Name + " (" + G(st, 16) + "-" + G(st, 17) + ", " + ((G(st, 11) <= 0) ? "-.--" : ((double)G(st, 13) * 27.0 / (double)G(st, 11)).ToString("0.00", CultureInfo.InvariantCulture)) + " ERA)";
		}
		case Sport.Football:
		{
			List<Player> qbs = byPos(Position.Quarterback);
			if (qbs.Count != 0)
			{
				return "Quarterback: " + qbs[0].Name;
			}
			return "";
		}
		case Sport.Soccer:
		{
			List<Player> gk = byPos(Position.Goalkeeper);
			if (gk.Count != 0)
			{
				return "Goalkeeper: " + gk[0].Name;
			}
			return "";
		}
		default:
			return "";
		}
	}

	private static List<KeyValuePair<Player, string>> TeamLeaders(SportsWorld world, Team t)
	{
		List<KeyValuePair<Player, string>> list = new List<KeyValuePair<Player, string>>();
		List<Player> roster = world.RosterOf(t.Id);
		Func<Player, int[]> st = (Player p) => Stats.Fit(p.Season, t.Sport);
		Action<Func<Player, bool>, Func<int[], double>, Func<int[], string>, int> top = (Func<Player, bool> who, Func<int[], double> value, Func<int[], string> text, int n) =>
		{
			List<Player> pool = roster.FindAll((Player p) => who(p) && value(st(p)) > 0.0);
			pool.Sort((Player a, Player b) => value(st(b)).CompareTo(value(st(a))));
			int i;
			for (i = 0; i < pool.Count && i < n; i++)
			{
				if (!list.Exists((KeyValuePair<Player, string> x) => x.Key == pool[i]))
				{
					list.Add(new KeyValuePair<Player, string>(pool[i], text(st(pool[i]))));
				}
			}
		};
		switch (t.Sport)
		{
		case Sport.Hockey:
			top((Player p) => p.Position != Position.Goalie, (int[] s) => (double)(G(s, 1) + G(s, 2)) + (double)G(s, 1) * 0.01, (int[] s) => G(s, 1) + " G, " + G(s, 2) + " A", 3);
			break;
		case Sport.Basketball:
			top((Player p) => true, (int[] s) => (G(s, 0) > 0) ? ((double)G(s, 1) / (double)G(s, 0)) : 0.0, (int[] s) => ((double)G(s, 1) / (double)Math.Max(1, G(s, 0))).ToString("0.0", CultureInfo.InvariantCulture) + " PPG, " + ((double)G(s, 2) / (double)Math.Max(1, G(s, 0))).ToString("0.0", CultureInfo.InvariantCulture) + " RPG", 3);
			break;
		case Sport.Football:
			top((Player p) => p.Position == Position.Quarterback, (int[] s) => G(s, 3), (int[] s) => G(s, 3) + " pass yds, " + G(s, 4) + " TD", 1);
			top((Player p) => true, (int[] s) => G(s, 7), (int[] s) => G(s, 7) + " rush yds, " + G(s, 8) + " TD", 1);
			top((Player p) => true, (int[] s) => G(s, 10), (int[] s) => G(s, 10) + " rec yds, " + G(s, 11) + " TD", 1);
			break;
		case Sport.Baseball:
			top((Player p) => !Rosters.IsPitcher(p.Position), (int[] s) => G(s, 5) * 10 + G(s, 6), (int[] s) => G(s, 5) + " HR, " + G(s, 6) + " RBI, " + ((G(s, 1) <= 0) ? ".000" : ((double)G(s, 3) / (double)G(s, 1)).ToString(".000", CultureInfo.InvariantCulture)), 3);
			break;
		default:
			top((Player p) => p.Position != Position.Goalkeeper, (int[] s) => G(s, 1) * 2 + G(s, 2), (int[] s) => G(s, 1) + " G, " + G(s, 2) + " A", 3);
			break;
		}
		return list;
	}

	private static string DayRange(Tournament t)
	{
		return GameClock.DateOf(t.StartDay).ToString("MMM d", CultureInfo.InvariantCulture) + " to " + GameClock.DateOf(t.StartDay + 10).ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
	}

	private static bool UserPlayer(SportsWorld world, int playerId)
	{
		Player p = world.GetPlayer(playerId);
		return ((p == null) ? null : world.GetTeam(p.TeamId))?.IsUser ?? false;
	}

	private static void WriteIntl(JsonBuilder j, SportsWorld world, string tab)
	{
		List<Tournament> list = new List<Tournament>(world.Tournaments);
		list.Sort((Tournament tournament, Tournament b) => b.StartDay.CompareTo(tournament.StartDay));
		Tournament sel = ((list.Count > 0) ? list[0] : null);
		foreach (Tournament t in list)
		{
			if (t.Id.ToString(CultureInfo.InvariantCulture) == tab)
			{
				sel = t;
			}
		}
		j.Name("intl").BeginObject();
		j.Name("country").BeginObject().Prop("name", world.HomeCountry)
			.Prop("abbr", world.HomeAbbrev)
			.Prop("custom", world.CustomCountry);
		j.Name("known").BeginArray();
		foreach (string c in SportsWorld.KnownCountries())
		{
			j.BeginObject().Prop("name", c).Prop("abbr", SportsWorld.Abbrev(c))
				.EndObject();
		}
		j.EndArray().EndObject();
		j.Name("upcoming").BeginArray();
		foreach (string u in world.UpcomingTournaments(world.LastTime))
		{
			j.Value(u);
		}
		j.EndArray();
		j.Name("list").BeginArray();
		foreach (Tournament t2 in list)
		{
			int mine = 0;
			foreach (NationTeam team in t2.Teams)
			{
				foreach (int id in team.PlayerIds)
				{
					if (UserPlayer(world, id))
					{
						mine++;
					}
				}
			}
			j.BeginObject().Prop("id", t2.Id).Prop("name", t2.Label)
				.Prop("sport", SportRules.SportLabel(t2.Sport))
				.Prop("status", t2.Complete ? ("Gold: " + t2.Gold) : DayRange(t2))
				.Prop("mine", mine)
				.EndObject();
		}
		j.EndArray();
		if (sel != null)
		{
			int qualifiers = 0;
			int field = 0;
			foreach (NationTeam n in sel.Teams)
			{
				if (n.Qualifier && n.Group >= 0)
				{
					qualifiers++;
				}
				if (n.Group >= 0)
				{
					field++;
				}
			}
			j.Prop("id", sel.Id);
			j.Prop("name", sel.Label);
			j.Prop("summary", "In " + sel.Host + ", " + DayRange(sel) + "." + (sel.Under20 ? " Under 20." : "") + ((field > 0) ? (" " + field + " nations" + ((qualifiers > 0) ? (" (" + qualifiers + " through the qualifiers)") : "") + ".") : " The qualifiers decide the last spots.") + " National teams are picked from the players in your sports world by where they're from; the spots nobody fills go to players from clubs abroad.");
			j.Prop("medals", sel.Complete ? ("Gold: " + sel.Gold + "  ·  Silver: " + sel.Silver + "  ·  Bronze: " + sel.Bronze) : "");
			j.Name("groups").BeginArray();
			int groups = field / 4;
			for (int g = 0; g < groups; g++)
			{
				List<int> list2 = world.GroupTable(sel, g);
				j.BeginObject().Prop("name", "Group " + (char)(65 + g));
				j.Name("rows").BeginArray();
				foreach (int i in list2)
				{
					int[] row = world.GroupRow(i);
					bool mine2 = sel.Teams[i].PlayerIds.Exists((int playerId) => UserPlayer(world, playerId));
					j.BeginObject().Prop("nation", sel.Teams[i].Nation).Prop("abbr", world.NationAbbrev(sel.Teams[i].Nation))
						.Prop("home", sel.Teams[i].Nation == world.HomeNation(sel.Sport))
						.Prop("gp", row[6])
						.Prop("w", row[3])
						.Prop("d", row[4])
						.Prop("l", row[5])
						.Prop("goals", row[2] + "-" + (row[2] - row[1]))
						.Prop("pts", row[0])
						.Prop("mine", mine2)
						.EndObject();
				}
				j.EndArray().EndObject();
			}
			j.EndArray();
			List<TourGame> list3 = new List<TourGame>(sel.Games);
			list3.Sort((TourGame tourGame, TourGame b) => (tourGame.Day == b.Day) ? tourGame.Minute.CompareTo(b.Minute) : tourGame.Day.CompareTo(b.Day));
			j.Name("games").BeginArray();
			foreach (TourGame g2 in list3)
			{
				NationTeam h = sel.Teams[g2.Home];
				NationTeam a = sel.Teams[g2.Away];
				string score = ((!g2.Played) ? "" : (g2.HomeScore + "-" + g2.AwayScore + ((g2.Suffix.Length > 0) ? (" " + g2.Suffix) : "") + ((g2.TieWinner >= 0) ? (" (" + sel.Teams[g2.TieWinner].Nation + " win the shootout)") : "")));
				j.BeginObject().Prop("stage", g2.Stage).Prop("date", GameClock.ShortDayLabel(g2.Day) + ", " + GameClock.ClockLabel(g2.Minute))
					.Prop("home", h.Nation)
					.Prop("away", a.Nation)
					.Prop("homeAbbr", world.NationAbbrev(h.Nation))
					.Prop("awayAbbr", world.NationAbbrev(a.Nation))
					.Prop("score", score)
					.Prop("played", g2.Played)
					.Prop("mine", h.PlayerIds.Exists((int playerId) => UserPlayer(world, playerId)) || a.PlayerIds.Exists((int playerId) => UserPlayer(world, playerId)) || h.Nation == world.HomeNation(sel.Sport) || a.Nation == world.HomeNation(sel.Sport))
					.EndObject();
			}
			j.EndArray();
			List<NationTeam> list4 = sel.Teams.FindAll((NationTeam nationTeam) => nationTeam.Group >= 0 || nationTeam.Qualifier);
			list4.Sort((NationTeam nationTeam, NationTeam b) => b.Rating.CompareTo(nationTeam.Rating));
			j.Name("teams").BeginArray();
			foreach (NationTeam n2 in list4)
			{
				JsonBuilder jsonBuilder = j.BeginObject().Prop("nation", n2.Nation).Prop("abbr", world.NationAbbrev(n2.Nation))
					.Prop("home", n2.Nation == world.HomeNation(sel.Sport));
				string text = ((n2.Group >= 0) ? ("Group " + (char)(65 + n2.Group)) : "Out in the qualifiers");
				string text2 = ((n2.Qualifier && n2.Group >= 0) ? ", through the qualifiers" : "");
				string text3;
				if (n2.Nation == sel.Gold)
				{
					text3 = ", gold";
				}
				else if (n2.Nation == sel.Silver)
				{
					text3 = ", silver";
				}
				else
				{
					text3 = ((n2.Nation == sel.Bronze) ? ", bronze" : "");
				}
				jsonBuilder.Prop("note", text + text2 + text3).Prop("unnamed", n2.Unnamed);
				j.Name("players").BeginArray();
				List<Player> players = new List<Player>();
				foreach (int id2 in n2.PlayerIds)
				{
					Player p = world.AnyPlayer(id2);
					if (p != null)
					{
						players.Add(p);
					}
				}
				players.Sort((Player player, Player b) => b.Overall.CompareTo(player.Overall));
				foreach (Player p2 in players)
				{
					Team club = world.GetTeam(p2.TeamId);
					JsonBuilder jsonBuilder2 = j.BeginObject().Prop("id", p2.Id).Prop("name", p2.Name)
						.Prop("pos", Rosters.PositionLabel(p2.Position))
						.Prop("ovr", Rosters.Ovr(p2));
					string value;
					if (club != null)
					{
						value = world.TeamName(club.Id) + " (" + SportRules.LevelLabel(club.Sport, club.Level) + ")";
					}
					else
					{
						value = ((p2.Status == PlayerStatus.FreeAgent) ? "Free agent" : "");
					}
					jsonBuilder2.Prop("club", value).Prop("mine", club?.IsUser ?? false).EndObject();
				}
				j.EndArray().EndObject();
			}
			j.EndArray();
		}
		j.EndObject();
	}

	private static List<KeyValuePair<string, string>> LeagueTabs(League league)
	{
		List<KeyValuePair<string, string>> tabs = new List<KeyValuePair<string, string>>
		{
			new KeyValuePair<string, string>("", "Standings"),
			new KeyValuePair<string, string>("schedule", "Scores and schedule"),
			new KeyValuePair<string, string>("leaders", "Stat leaders"),
			new KeyValuePair<string, string>("calendar", "Calendar"),
			new KeyValuePair<string, string>("coaches", "Coaches")
		};
		if (league.Level == Level.Pro && SportsWorld.HasDraft(league.Sport))
		{
			tabs.Add(new KeyValuePair<string, string>("draft", "Draft"));
		}
		if (SportRules.ProLike(league.Sport, league.Level))
		{
			tabs.Add(new KeyValuePair<string, string>("fa", "Free agents"));
		}
		tabs.Add(new KeyValuePair<string, string>("awards", "Awards"));
		if (SportsWorld.HasAllStars(league))
		{
			tabs.Add(new KeyValuePair<string, string>("allstars", "All-Stars"));
		}
		tabs.Add(new KeyValuePair<string, string>("records", "Record book"));
		if (league.Level == Level.Pro || league.Level == Level.College)
		{
			tabs.Add(new KeyValuePair<string, string>("hall", "Hall of Fame"));
		}
		tabs.Add(new KeyValuePair<string, string>("history", "Past seasons"));
		return tabs;
	}

	private static void WriteLeaguePage(JsonBuilder j, SportsWorld world, League league, int now, string tab)
	{
		List<KeyValuePair<string, string>> tabs = LeagueTabs(league);
		if (!tabs.Exists((KeyValuePair<string, string> keyValuePair) => keyValuePair.Key == tab))
		{
			tab = "";
		}
		Season s = league.Current;
		bool pro = SportRules.ProLike(league.Sport, league.Level);
		j.Name("league").BeginObject();
		j.Prop("key", league.Key);
		j.Prop("title", LeagueTitle(league));
		j.Prop("isPro", pro);
		JsonBuilder jsonBuilder = j;
		string value;
		if (s != null)
		{
			value = world.SeasonLabel(league, s);
		}
		else
		{
			value = ((league.Previous == null) ? "" : world.SeasonLabel(league, league.Previous));
		}
		jsonBuilder.Prop("seasonLabel", value);
		string status = LeagueStatus(world, league, now);
		int planned = world.PlannedSize(league);
		if (league.Current != null && planned != league.TeamIds.Count)
		{
			status = status + " Next season the league " + ((planned > league.TeamIds.Count) ? "grows" : "shrinks") + " to " + planned + " teams (Teams per league setting)" + ((planned < league.TeamIds.Count) ? ": the weakest out of town teams fold." : ": expansion teams join.");
		}
		j.Prop("status", status);
		j.Prop("tab", tab);
		j.Name("tabs").BeginArray();
		foreach (KeyValuePair<string, string> t in tabs)
		{
			j.BeginObject().Prop("id", t.Key).Prop("label", t.Value)
				.EndObject();
		}
		j.EndArray();
		switch (tab)
		{
		case "schedule":
			WriteSchedulePage(j, world, league, now);
			break;
		case "leaders":
			WriteBoards(j, world, league);
			break;
		case "calendar":
			WriteCalendar(j, world, league, now);
			break;
		case "coaches":
			WriteCoachesPage(j, world, league);
			break;
		case "draft":
			WriteDraft(j, world, league, now);
			break;
		case "fa":
			WriteFreeAgents(j, world, league, now);
			break;
		case "awards":
			WriteAwards(j, world, league);
			break;
		case "allstars":
			WriteAllStars(j, world, league);
			break;
		case "hall":
			WriteHallPage(j, world, league);
			break;
		case "records":
			WriteRecords(j, world, league);
			break;
		case "history":
			WriteHistory(j, league);
			break;
		default:
			WriteStandingsPage(j, world, league, now);
			break;
		}
		j.EndObject();
	}

	private static void WriteStandingsPage(JsonBuilder j, SportsWorld world, League league, int now)
	{
		Season s = league.Current;
		SportProfile profile = SportsWorld.Profile(league);
		bool showPrevious = league.Previous != null && (s == null || !s.AnyPlayed);
		Season shown = (showPrevious ? league.Previous : s);
		j.Prop("summary", profile.Summary);
		j.Prop("standingsTitle", showPrevious ? ("Final standings " + world.SeasonLabel(league, shown)) : "Standings");
		List<StandingRow> table = ((shown == null) ? new List<StandingRow>() : Standings.Compute(league, shown));
		int playoffTeams = SportsWorld.PlayoffTeamCount(profile, table.Count);
		JsonBuilder jsonBuilder = j;
		string value;
		if (playoffTeams <= 0)
		{
			value = "";
		}
		else
		{
			value = ((playoffTeams >= table.Count) ? "Every team makes the playoffs (seeded by the standings)." : ("The top " + playoffTeams + " make the playoffs."));
		}
		jsonBuilder.Prop("playoffNote", value);
		League second = ((league.Sport == Sport.Soccer) ? world.SecondDivision() : null);
		bool moved = showPrevious && second != null && shown != null && second.PromotionDoneFor >= shown.Year;
		j.Prop("zoneNote", world.DivisionNote(league));
		j.Name("columns").BeginArray();
		string[] array = Columns(league.Sport);
		foreach (string c in array)
		{
			j.Value(c);
		}
		j.EndArray();
		j.Name("standings").BeginArray();
		List<int> userTeams = new List<int>();
		foreach (StandingRow row in table)
		{
			Team u = world.GetTeam(row.TeamId);
			if (u != null && u.IsUser)
			{
				userTeams.Add(u.Id);
			}
		}
		foreach (StandingRow row2 in table)
		{
			Team t = world.GetTeam(row2.TeamId);
			j.BeginObject();
			j.Prop("rank", row2.Rank);
			j.Prop("teamId", row2.TeamId);
			j.Prop("name", world.TeamName(row2.TeamId));
			j.Prop("abbr", world.TeamAbbrev(row2.TeamId));
			j.Prop("isUser", t?.IsUser ?? false);
			j.Prop("playoffSpot", playoffTeams < table.Count && row2.Rank <= playoffTeams);
			string zone;
			if (moved)
			{
				if (league.Level != Level.Pro)
				{
					zone = (second.LastUp.Contains(row2.TeamId) ? "up" : "");
				}
				else
				{
					zone = (second.LastDown.Contains(row2.TeamId) ? "down" : "");
				}
			}
			else
			{
				zone = world.DivisionZone(league, row2.Rank, table.Count);
			}
			if (zone.Length > 0)
			{
				j.Prop("zone", zone);
			}
			j.Prop("hot", t != null && t.Cinderella > 0 && !showPrevious);
			j.Prop("rival", RivalOfUser(world, league, row2.TeamId, userTeams));
			j.Name("cells").BeginArray();
			array = Cells(league.Sport, row2);
			foreach (string cell in array)
			{
				j.Value(cell);
			}
			j.EndArray();
			j.EndObject();
		}
		j.EndArray();
		if (shown != null && shown.PlayoffsCreated && shown.Bracket.Count > 0)
		{
			WritePlayoffs(j, world, league, shown);
		}
		j.Name("tonight").BeginArray();
		if (s != null)
		{
			List<Match> tonight = new List<Match>();
			int nextMonth = GameClock.NextMonthStartDay(now);
			foreach (Match g in s.Games)
			{
				if (g.Featured && !g.Played && GameClock.DayOf(g.Time) < nextMonth)
				{
					tonight.Add(g);
				}
			}
			tonight.Sort((Match a, Match b) => GameClock.MinuteOfDay(a.Time).CompareTo(GameClock.MinuteOfDay(b.Time)));
			foreach (Match g2 in tonight)
			{
				WriteGame(j, world, league, g2);
			}
		}
		j.EndArray();
	}

	private static string RivalOfUser(SportsWorld world, League league, int teamId, List<int> userTeams)
	{
		Rivalry best = null;
		foreach (int u in userTeams)
		{
			if (u != teamId)
			{
				Rivalry r = world.FindRivalry(league, teamId, u, create: false);
				if (r != null && r.Heat >= 50 && (best == null || r.Heat > best.Heat))
				{
					best = r;
				}
			}
		}
		return RivalTagText(best, game: false);
	}

	public static string RivalTagText(Rivalry r, bool game)
	{
		if (r == null || r.Heat < 50)
		{
			return "";
		}
		string s;
		if (r.Heat >= 80)
		{
			s = "Bitter rival";
		}
		else
		{
			s = (r.Crosstown ? "Crosstown rival" : "Rival");
		}
		if (!game)
		{
			return s;
		}
		return s + "s";
	}

	private static void WriteAllStars(JsonBuilder j, SportsWorld world, League league)
	{
		Sport sport = league.Sport;
		j.Prop("allStarNote", sport switch
		{
			Sport.Hockey => "The All-Star Game rosters are picked past the halfway mark: forwards, defensemen and goalies, and every team sends at least one player. At the end of the regular season come the First and Second All-Star Teams: a center, a left wing, a right wing, two defensemen and a goalie.", 
			Sport.Basketball => "All-Stars are picked about two thirds of the way through the season: 10 starters (fans, players and media) and the reserves (the coaches), without regard to position. The three All-League teams of five come at the end of the regular season; a player needs 65 of 82 games (or the same share of a shorter season) to be picked.", 
			_ => "The Pro Bowl roster is picked late in the season, by position. At the end of the regular season come the First and Second Team All-Pro, by position.", 
		});
		List<int> years = new List<int>();
		foreach (AllStarPick k in league.AllStars)
		{
			if (!years.Contains(k.Year))
			{
				years.Add(k.Year);
			}
		}
		years.Sort((int a, int b) => b.CompareTo(a));
		j.Name("allStarSeasons").BeginArray();
		foreach (int year in years)
		{
			List<AllStarPick> picks = league.AllStars.FindAll((AllStarPick allStarPick) => allStarPick.Year == year);
			j.BeginObject().Prop("year", year).Prop("season", picks[0].Season);
			j.Name("groups").BeginArray();
			Action<string, List<AllStarPick>> group = (string title, List<AllStarPick> list) =>
			{
				if (list.Count != 0)
				{
					j.BeginObject().Prop("title", title);
					j.Name("rows").BeginArray();
					foreach (AllStarPick current in list)
					{
						Team team = world.GetTeam(current.TeamId);
						j.BeginObject().Prop("playerId", (world.AnyPlayer(current.PlayerId) != null) ? current.PlayerId : (-1)).Prop("name", current.Name)
							.Prop("pos", current.Pos)
							.Prop("team", current.Team)
							.Prop("teamId", current.TeamId)
							.Prop("detail", current.Detail)
							.Prop("starter", current.Starter)
							.Prop("isUser", team?.IsUser ?? false)
							.EndObject();
					}
					j.EndArray();
					j.EndObject();
				}
			};
			int kind;
			for (kind = 1; kind <= 3; kind++)
			{
				group(SportsWorld.AllLeagueName(sport, kind), picks.FindAll((AllStarPick allStarPick) => allStarPick.Kind == kind));
			}
			List<AllStarPick> game = picks.FindAll((AllStarPick allStarPick) => allStarPick.Kind == 0);
			if (sport == Sport.Basketball)
			{
				group(SportsWorld.AllStarGameName(sport) + ": starters", game.FindAll((AllStarPick allStarPick) => allStarPick.Starter));
				group(SportsWorld.AllStarGameName(sport) + ": reserves", game.FindAll((AllStarPick allStarPick) => !allStarPick.Starter));
			}
			else
			{
				group(SportsWorld.AllStarGameName(sport), game);
			}
			j.EndArray();
			j.EndObject();
		}
		j.EndArray();
	}

	private static void WriteSchedulePage(JsonBuilder j, SportsWorld world, League league, int now)
	{
		j.Name("live").BeginArray();
		if (league.Current != null)
		{
			List<Match> list = league.Current.Games.FindAll((Match match) => !match.Played && match.Live != null);
			list.Sort((Match a, Match b) => (a.Live.Start == b.Live.Start) ? a.Id.CompareTo(b.Id) : a.Live.Start.CompareTo(b.Live.Start));
			foreach (Match g in list)
			{
				WriteGame(j, world, league, g);
			}
		}
		j.EndArray();
		j.Name("recent").BeginArray();
		foreach (KeyValuePair<int, List<Match>> day in RecentDays(league, 4))
		{
			WriteDay(j, world, league, day);
		}
		j.EndArray();
		j.Name("upcoming").BeginArray();
		if (league.Current != null)
		{
			foreach (KeyValuePair<int, List<Match>> day2 in UpcomingDays(league.Current, 4, 30))
			{
				WriteDay(j, world, league, day2);
			}
		}
		j.EndArray();
	}

	private static void WriteBoards(JsonBuilder j, SportsWorld world, League league)
	{
		bool last = world.ShowingLastSeason(league);
		Season shown = (last ? league.Previous : league.Current);
		int teamGames = 0;
		if (shown != null)
		{
			foreach (StandingRow r in Standings.Compute(league, shown))
			{
				teamGames = Math.Max(teamGames, r.Played);
			}
		}
		JsonBuilder jsonBuilder = j;
		string value;
		if (last)
		{
			value = ((league.Previous != null) ? ("Final " + world.SeasonLabel(league, league.Previous) + " stats") : "No games played yet");
		}
		else
		{
			value = world.SeasonLabel(league, league.Current) + " season";
		}
		jsonBuilder.Prop("boardsTitle", value);
		int year = world.CurrentYear();
		List<Player> players = new List<Player>();
		foreach (Player p in world.Players.Values)
		{
			if (p.Sport == league.Sport && p.Level == league.Level && (p.Status == PlayerStatus.Active || p.Status == PlayerStatus.Prospect || p.Status == PlayerStatus.FreeAgent))
			{
				players.Add(p);
			}
		}
		j.Name("boards").BeginArray();
		Stats.Leader[] array = Stats.Leaders(league.Sport, teamGames);
		foreach (Stats.Leader board in array)
		{
			List<KeyValuePair<Player, double>> rows = new List<KeyValuePair<Player, double>>();
			foreach (Player p2 in players)
			{
				int[] line = Stats.Fit(last ? p2.LastSeason : p2.Season, league.Sport);
				if (!Stats.IsEmpty(line) && board.Qualifies(p2, line))
				{
					rows.Add(new KeyValuePair<Player, double>(p2, board.Value(line)));
				}
			}
			rows.Sort((KeyValuePair<Player, double> a, KeyValuePair<Player, double> b) =>
			{
				int num = (board.LowerIsBetter ? a.Value.CompareTo(b.Value) : b.Value.CompareTo(a.Value));
				return (num == 0) ? a.Key.Id.CompareTo(b.Key.Id) : num;
			});
			j.BeginObject();
			j.Prop("title", board.Title);
			j.Name("rows").BeginArray();
			for (int i2 = 0; i2 < rows.Count && i2 < 25; i2++)
			{
				Player p3 = rows[i2].Key;
				Team team = world.GetTeam(p3.TeamId);
				j.BeginObject().Prop("rank", i2 + 1).Prop("playerId", p3.Id)
					.Prop("name", p3.Name)
					.Prop("pos", Rosters.PositionLabel(p3.Position))
					.Prop("age", (league.Level == Level.Pro || league.Level == Level.Junior || league.Level == Level.Minor) ? (year - p3.BirthYear).ToString() : Rosters.ClassLabel(p3.ClassYear))
					.Prop("teamId", team?.Id ?? (-1))
					.Prop("team", (team == null) ? "Free agent" : world.TeamAbbrev(team.Id))
					.Prop("value", board.Format(rows[i2].Value))
					.Prop("isUser", team?.IsUser ?? false)
					.EndObject();
			}
			j.EndArray();
			j.EndObject();
		}
		j.EndArray();
	}

	private static void WriteCalendar(JsonBuilder j, SportsWorld world, League league, int now)
	{
		j.Name("windows").BeginArray();
		foreach (string line in world.Windows(league, now))
		{
			j.Value(line);
		}
		j.EndArray();
		j.Name("events").BeginArray();
		foreach (CalendarEvent ev in world.Calendar(league, now))
		{
			j.BeginObject().Prop("date", ev.Date).Prop("title", ev.Title)
				.Prop("detail", ev.Detail)
				.Prop("state", ev.State)
				.EndObject();
		}
		j.EndArray();
	}

	private static void WriteCoachesPage(JsonBuilder j, SportsWorld world, League league)
	{
		Season s = league.Current ?? league.Previous;
		List<int> order = new List<int>();
		Dictionary<int, string> records = new Dictionary<int, string>();
		if (s != null)
		{
			foreach (StandingRow r in Standings.Compute(league, s))
			{
				order.Add(r.TeamId);
				records[r.TeamId] = Standings.RecordText(league.Sport, r);
			}
		}
		foreach (int id in league.TeamIds)
		{
			if (!order.Contains(id))
			{
				order.Add(id);
			}
		}
		int year = world.CurrentYear();
		j.Prop("coachNote", (league.Sport == Sport.Football) ? "Football teams have a head coach plus offensive and defensive coordinators. Every other sport has a head coach. Click a team to see its staff." : "Every team has a head coach. Click a team to see its staff.");
		j.Name("teams").BeginArray();
		foreach (int id2 in order)
		{
			Team t = world.GetTeam(id2);
			if (t == null || t.Retired || !league.TeamIds.Contains(id2))
			{
				continue;
			}
			records.TryGetValue(id2, out var rec);
			j.BeginObject();
			j.Prop("teamId", id2);
			j.Prop("team", world.TeamName(id2));
			j.Prop("isUser", t.IsUser);
			j.Prop("record", rec ?? "");
			j.Name("staff").BeginArray();
			foreach (Coach c in world.StaffOf(id2))
			{
				WriteCoachLine(j, world, c, year);
			}
			j.EndArray();
			j.EndObject();
		}
		j.EndArray();
		j.Name("available").BeginArray();
		List<Coach> pool = world.AvailableCoaches(league.Sport, league.Level);
		for (int i = 0; i < pool.Count && i < 8; i++)
		{
			WriteCoachLine(j, world, pool[i], year);
		}
		j.EndArray();
	}

	private static void WriteCoachLine(JsonBuilder j, SportsWorld world, Coach c, int year)
	{
		Player stand = new Player
		{
			Hometown = (c.Hometown ?? "")
		};
		j.BeginObject().Prop("hometown", Bios.HometownLabel(stand, world.CityName)).Prop("exPro", !string.IsNullOrEmpty(c.Played))
			.Prop("playerId", (c.FormerPlayerId >= 0 && world.Retired.ContainsKey(c.FormerPlayerId)) ? c.FormerPlayerId : (-1))
			.Prop("id", c.Id)
			.Prop("role", SportsWorld.RoleLabel(c.Role, c.Sport))
			.Prop("roleId", (int)c.Role)
			.Prop("name", c.Name)
			.Prop("age", year - c.BirthYear)
			.Prop("ovr", c.Overall)
			.Prop("grade", SportsWorld.CoachGrade(c))
			.Prop("record", CoachRecord(c))
			.Prop("titles", c.Titles)
			.EndObject();
	}

	public static string CoachRecord(Coach c)
	{
		string r = c.Wins + "-" + c.Losses + ((c.Ties > 0) ? ("-" + c.Ties) : "");
		if (c.Wins + c.Losses + c.Ties != 0)
		{
			return r;
		}
		return "No games yet";
	}

	private static void WriteAwards(JsonBuilder j, SportsWorld world, League league)
	{
		j.Name("seasons").BeginArray();
		string current = null;
		int seasons = 0;
		foreach (AwardRecord a in league.Awards)
		{
			if (a.SeasonLabel != current)
			{
				if (current != null)
				{
					j.EndArray().EndObject();
				}
				if (++seasons > 10)
				{
					current = null;
					break;
				}
				current = a.SeasonLabel;
				j.BeginObject().Prop("season", a.SeasonLabel);
				j.Name("awards").BeginArray();
			}
			j.BeginObject().Prop("award", a.Award).Prop("trophy", a.Trophy)
				.Prop("player", a.PlayerName)
				.Prop("playerId", world.Players.ContainsKey(a.PlayerId) ? a.PlayerId : (-1))
				.Prop("teamId", a.TeamId)
				.Prop("team", a.TeamName)
				.Prop("detail", a.Detail)
				.Prop("isUser", a.IsUser)
				.EndObject();
		}
		if (current != null)
		{
			j.EndArray().EndObject();
		}
		j.EndArray();
		j.Name("trophies").BeginArray();
		j.BeginObject().Prop("award", "Champion").Prop("trophy", AwardBook.ChampionTrophy(league.Sport, league.Level))
			.EndObject();
		foreach (AwardDef def in AwardBook.For(league.Sport, league.Level))
		{
			j.BeginObject().Prop("award", def.Award).Prop("trophy", def.Trophy)
				.EndObject();
		}
		j.EndArray();
	}

	private static void WriteRecords(JsonBuilder j, SportsWorld world, League league)
	{
		HashSet<string> userTeams = new HashSet<string>();
		foreach (Team t in world.UserTeams())
		{
			userTeams.Add(world.TeamName(t.Id));
		}
		string[] groups = new string[4] { "Single season", "Career", "Awards", "Teams" };
		string[] notes = new string[4] { "The best single regular seasons ever in this league. A record stays until someone beats it.", "Career totals while playing in this league (regular season). Rate stats need about four full seasons to qualify.", "Most awards won.", "Championships and the best regular season records." };
		j.Name("groups").BeginArray();
		for (int g = 0; g < groups.Length; g++)
		{
			j.BeginObject();
			j.Prop("name", groups[g]);
			j.Prop("note", notes[g]);
			j.Name("boards").BeginArray();
			foreach (RecordBoard b in league.Records)
			{
				if (!(b.Group != groups[g]) && b.Entries.Count != 0)
				{
					j.BeginObject();
					j.Prop("title", b.Title);
					j.Name("rows").BeginArray();
					for (int i = 0; i < b.Entries.Count && i < 10; i++)
					{
						RecordEntry e = b.Entries[i];
						j.BeginObject().Prop("rank", i + 1).Prop("name", e.Name)
							.Prop("team", e.Team)
							.Prop("season", e.Season)
							.Prop("value", e.Text)
							.Prop("isUser", userTeams.Contains(e.Team) || (groups[g] == "Teams" && userTeams.Contains(e.Name)))
							.EndObject();
					}
					j.EndArray();
					j.EndObject();
				}
			}
			j.EndArray();
			j.EndObject();
		}
		j.EndArray();
	}

	private static void WriteHistory(JsonBuilder j, League league)
	{
		j.Name("champions").BeginArray();
		for (int i = 0; i < league.Champions.Count && i < 20; i++)
		{
			ChampionRecord c = league.Champions[i];
			j.BeginObject().Prop("season", c.SeasonLabel).Prop("team", c.TeamName)
				.Prop("runnerUp", c.RunnerUpName)
				.Prop("score", c.FinalScore)
				.Prop("isUser", c.IsUser)
				.EndObject();
		}
		j.EndArray();
		j.Name("seasons").BeginArray();
		foreach (SeasonSummary h in league.History)
		{
			j.BeginObject();
			j.Prop("season", h.Season);
			j.Prop("champion", h.Champion);
			j.Prop("runnerUp", h.RunnerUp);
			j.Prop("finalScore", h.FinalScore);
			j.Prop("championIsUser", h.ChampionIsUser);
			WriteLines(j, "notes", h.Notes);
			WriteLines(j, "awards", h.Awards);
			WriteLines(j, "leaders", h.Leaders);
			WriteLines(j, "playoffs", h.Playoffs);
			WriteLines(j, "standings", h.Standings);
			j.EndObject();
		}
		j.EndArray();
	}

	private static void WriteLines(JsonBuilder j, string name, List<string> lines)
	{
		j.Name(name).BeginArray();
		foreach (string line in lines)
		{
			j.Value(line);
		}
		j.EndArray();
	}

	private static void WriteDraft(JsonBuilder j, SportsWorld world, League league, int now)
	{
		EconomyProfile e = Economy.Get(league.Sport);
		DraftState d = league.Draft;
		int year = world.CurrentYear();
		j.Prop("draftName", e.DraftName);
		j.Prop("rounds", e.DraftRounds);
		j.Prop("lotteryInfo", LotteryInfo(e));
		if (d == null)
		{
			j.Prop("draftTitle", e.DraftName);
			j.Prop("draftStatus", "No draft yet. " + world.OffseasonNote(league, now));
		}
		else
		{
			string name = SportsWorld.DraftYearLabel(league, d.Year) + " " + e.DraftName;
			j.Prop("draftTitle", name);
			DraftPick next = ((d.Started && !d.Complete) ? d.NextPick : null);
			Team onClock = ((next == null) ? null : world.GetTeam(next.TeamId));
			string status;
			if (d.Complete)
			{
				status = "The draft is over: " + d.Picks.Count + " picks made.";
			}
			else
			{
				status = (d.Started ? ("On the clock: pick No. " + next.Number + ", " + world.TeamName(next.TeamId) + ".") : ("Lottery done. The draft order for playoff teams is set when the playoffs end. " + world.OffseasonNote(league, now)));
			}
			j.Prop("draftStatus", status);
			j.Prop("userTurn", onClock != null && onClock.Manual && onClock.IsUser);
			j.Prop("userTeamId", (onClock != null && onClock.Manual) ? onClock.Id : (-1));
			j.Name("lottery").BeginArray();
			foreach (string line in d.Lottery)
			{
				j.Value(line);
			}
			j.EndArray();
			j.Name("order").BeginArray();
			for (int i = 0; i < d.Order.Count; i++)
			{
				Team t = world.GetTeam(d.Order[i]);
				j.BeginObject().Prop("number", i + 1).Prop("teamId", d.Order[i])
					.Prop("team", world.TeamName(d.Order[i]))
					.Prop("isUser", t?.IsUser ?? false)
					.EndObject();
			}
			j.EndArray();
			j.Name("picks").BeginArray();
			foreach (DraftPick p in d.Picks)
			{
				Team t2 = world.GetTeam(p.TeamId);
				bool traded = p.OriginalTeamId >= 0 && p.OriginalTeamId != p.TeamId;
				j.BeginObject().Prop("number", p.Number).Prop("round", p.Round)
					.Prop("teamId", p.TeamId)
					.Prop("team", world.TeamName(p.TeamId) + (traded ? (" (from " + world.TeamShortName(p.OriginalTeamId) + ")") : ""))
					.Prop("isUser", t2?.IsUser ?? false)
					.Prop("made", p.PlayerId >= 0)
					.Prop("playerId", p.PlayerId)
					.Prop("player", p.PlayerName)
					.Prop("pos", p.Position)
					.Prop("ovr", (p.Overall > 0) ? Rosters.Display(league.Sport, Level.Pro, p.Overall) : 0)
					.Prop("grade", p.Grade)
					.Prop("from", p.From)
					.EndObject();
			}
			j.EndArray();
		}
		j.Name("yourPicks").BeginArray();
		foreach (Team t3 in world.UserTeams())
		{
			if (world.LeagueOf(t3) != league)
			{
				continue;
			}
			foreach (PickAsset a in world.TradeablePicks(league, t3.Id))
			{
				j.BeginObject().Prop("label", world.PickLabel(league, a)).Prop("projection", world.PickProjection(league, a))
					.Prop("team", world.TeamShortName(t3.Id))
					.EndObject();
			}
		}
		j.EndArray();
		List<Player> pool = new List<Player>(world.DraftPool(league.Sport));
		pool.Sort((Player player, Player b) =>
		{
			int num = Rosters.Projected(player, year - player.BirthYear);
			int num2 = Rosters.Projected(b, year - b.BirthYear);
			return (num2 == num) ? b.Overall.CompareTo(player.Overall) : num2.CompareTo(num);
		});
		j.Name("prospects").BeginArray();
		for (int i2 = 0; i2 < pool.Count && i2 < 60; i2++)
		{
			Player p2 = pool[i2];
			int age = year - p2.BirthYear;
			j.BeginObject().Prop("id", p2.Id).Prop("rank", i2 + 1)
				.Prop("name", p2.Name)
				.Prop("pos", Rosters.PositionLabel(p2.Position))
				.Prop("age", age.ToString())
				.Prop("ovr", Rosters.Ovr(p2))
				.Prop("grade", Rosters.Grade(p2, age))
				.Prop("from", (string.IsNullOrEmpty(p2.From) || p2.From == "Prospect") ? "" : p2.From)
				.Prop("local", world.IsCityPlayer(p2))
				.EndObject();
		}
		j.EndArray();
	}

	private static string LotteryInfo(EconomyProfile e)
	{
		if (e.LotteryOdds == null)
		{
			return "No lottery: the worst record picks first, the champion picks last. Picks can be traded.";
		}
		string draws = ((e.LotteryPicks == 1) ? "the first pick is" : ("the first " + e.LotteryPicks + " picks are"));
		return "Non-playoff teams enter a lottery and " + draws + " drawn, with the worst records getting the best odds. Playoff teams follow, the champion last. Picks can be traded.";
	}

	private static void WriteFreeAgents(JsonBuilder j, SportsWorld world, League league, int now)
	{
		int year = world.CurrentYear();
		List<Team> signers = new List<Team>();
		j.Name("signers").BeginArray();
		foreach (int id in league.TeamIds)
		{
			Team t = world.GetTeam(id);
			if (t != null && t.IsUser && t.Manual)
			{
				signers.Add(t);
				j.BeginObject().Prop("id", id).Prop("name", world.TeamName(id))
					.Prop("capLine", CapLine(world, t))
					.EndObject();
			}
		}
		j.EndArray();
		List<Player> fas = new List<Player>(world.FreeAgents(league.Sport));
		fas.RemoveAll((Player player) => player.Level != Level.Pro);
		fas.Sort((Player a, Player b) => (b.Overall == a.Overall) ? a.Id.CompareTo(b.Id) : b.Overall.CompareTo(a.Overall));
		j.Prop("count", fas.Count);
		List<string> positions = new List<string>();
		foreach (Player item in fas)
		{
			string pos = Rosters.PositionLabel(item.Position);
			if (!positions.Contains(pos))
			{
				positions.Add(pos);
			}
		}
		j.Name("positions").BeginArray();
		RosterSlot[] array = Rosters.Template(league.Sport, Level.Pro);
		for (int num = 0; num < array.Length; num++)
		{
			string pos2 = Rosters.PositionLabel(array[num].Pos);
			if (positions.Contains(pos2))
			{
				j.Value(pos2);
			}
		}
		j.EndArray();
		j.Name("players").BeginArray();
		for (int i = 0; i < fas.Count && i < 100; i++)
		{
			Player p = fas[i];
			int age = year - p.BirthYear;
			int years = Economy.AskingYears(league.Sport, age, resign: false);
			j.BeginObject().Prop("id", p.Id).Prop("name", p.Name)
				.Prop("pos", Rosters.PositionLabel(p.Position))
				.Prop("age", age.ToString())
				.Prop("ovr", Rosters.Ovr(p))
				.Prop("grade", Rosters.Grade(p, age))
				.Prop("injury", world.InjuryText(p))
				.Prop("priority", SportsWorld.PriorityLabel(p.Priority))
				.Prop("asking", Economy.Money(Economy.AskingSalary(p, age, GameClock.MonthIndex(now))) + " x " + years + ((years == 1) ? " yr" : " yrs"))
				.Prop("from", (string.IsNullOrEmpty(p.From) || p.From == "Prospect") ? "" : p.From)
				.Prop("deciding", (p.DecideMonth >= 0) ? "Picks a team next month" : "");
			if (signers.Count > 0 && i < 40)
			{
				j.Prop("suitors", world.Suitors(p, now));
			}
			j.Name("offers").BeginArray();
			foreach (Team t2 in signers)
			{
				ContractTalk k = world.TalkOrPreview(p, t2, extension: false, now);
				bool refuses = k.Interest <= 0;
				JsonBuilder jsonBuilder = j.BeginObject().Prop("teamId", t2.Id).Prop("team", world.TeamShortName(t2.Id));
				string value;
				if (refuses)
				{
					value = "Not interested";
				}
				else
				{
					value = ((k.Status == 2) ? "Done talking" : (Economy.Money(k.Ask) + " x " + k.AskYears + ((k.AskYears == 1) ? " yr" : " yrs")));
				}
				jsonBuilder.Prop("asking", value).Prop("interested", !refuses && k.Status != 2).Prop("note", PriorityNote(p, refuses));
				WriteTalk(j, world, k, p, t2);
				j.EndObject();
			}
			j.EndArray();
			j.EndObject();
		}
		j.EndArray();
	}

	private static string PriorityNote(Player p, bool refuses)
	{
		if (refuses)
		{
			return "Wants a contender";
		}
		return p.Priority switch
		{
			PlayerPriority.Winning => "Wants to win", 
			PlayerPriority.Loyalty => "Loyal: gives his teams a discount", 
			PlayerPriority.Money => "Follows the money", 
			PlayerPriority.PlayingTime => "Wants to start", 
			PlayerPriority.Hometown => "Wants to play at home", 
			_ => "", 
		};
	}

	public static LiveSnapshot Live(Sport sport, Level level, Match g, int now)
	{
		LiveGame live = g.Live;
		if (live == null || live.Box == null)
		{
			return null;
		}
		LiveSnapshot snap = new LiveSnapshot();
		double f = (snap.Progress = ((live.End <= live.Start) ? 1.0 : Math.Max(0.0, Math.Min(1.0, (double)(now - live.Start) / (double)(live.End - live.Start)))));
		GameBox final = live.Box;
		int periods = Math.Max(final.HomePeriods.Count, final.AwayPeriods.Count);
		bool shootout = sport == Sport.Hockey && final.Note.StartsWith("Shootout");
		List<double> lengths = new List<double>();
		for (int p = 1; p <= periods; p++)
		{
			lengths.Add(PeriodLength(sport, level, p, periods, shootout, live.Playoff));
		}
		if (sport == Sport.Baseball)
		{
			lengths.Clear();
			for (int i = 1; i <= periods * 2; i++)
			{
				bool skipped = i % 2 == 0 && i / 2 - 1 < final.HomePeriods.Count && final.HomePeriods[i / 2 - 1] < 0;
				lengths.Add((!skipped) ? 1 : 0);
			}
		}
		double total = 0.0;
		foreach (double l in lengths)
		{
			total += l;
		}
		double elapsed = f * total;
		int current = 0;
		double into = 0.0;
		double start = 0.0;
		for (int j = 0; j < lengths.Count; j++)
		{
			if (!(lengths[j] <= 0.0))
			{
				current = j;
				if (elapsed < start + lengths[j])
				{
					into = elapsed - start;
					break;
				}
				start += lengths[j];
				into = lengths[j];
			}
		}
		snap.Status = ((f >= 1.0) ? "Final" : StatusText(sport, level, current + 1, into, (lengths.Count > current) ? lengths[current] : 0.0, periods, shootout));
		Dictionary<int, int> countInHalf = new Dictionary<int, int>();
		foreach (BoxEvent e in final.Events)
		{
			countInHalf.TryGetValue(e.Period, out var n);
			countInHalf[e.Period] = n + 1;
		}
		Dictionary<int, int> seenInHalf = new Dictionary<int, int>();
		foreach (BoxEvent e2 in final.Events)
		{
			double pos;
			if (sport == Sport.Baseball)
			{
				seenInHalf.TryGetValue(e2.Period, out var n2);
				seenInHalf[e2.Period] = n2 + 1;
				double before = 0.0;
				for (int k = 0; k < e2.Period - 1 && k < lengths.Count; k++)
				{
					before += lengths[k];
				}
				pos = before + (double)(n2 + 1) / (double)(countInHalf[e2.Period] + 1);
			}
			else
			{
				double before2 = 0.0;
				for (int m = 0; m < e2.Period - 1 && m < lengths.Count; m++)
				{
					before2 += lengths[m];
				}
				pos = before2 + EventSeconds(sport, e2);
			}
			if (pos <= elapsed || f >= 1.0)
			{
				snap.Box.Events.Add(e2);
			}
		}
		FootballProgress fp = null;
		if (sport == Sport.Football && final.Flow.Count >= 8)
		{
			fp = FootballDrives(final.Flow, elapsed, f >= 1.0, snap);
		}
		int hs = 0;
		int aws = 0;
		if (sport == Sport.Basketball && final.Flow.Count >= 2)
		{
			double boundary = 0.0;
			for (int num = 0; num < periods; num++)
			{
				bool started = num <= current || f >= 1.0;
				double end = Math.Min((f >= 1.0) ? total : elapsed, boundary + ((num < lengths.Count) ? lengths[num] : 0.0));
				ScoreAt(final.Flow, boundary, out var h0, out var a0);
				ScoreAt(final.Flow, end, out var h1, out var a1);
				if (f >= 1.0)
				{
					h1 = h0 + ((num < final.HomePeriods.Count) ? final.HomePeriods[num] : 0);
					a1 = a0 + ((num < final.AwayPeriods.Count) ? final.AwayPeriods[num] : 0);
				}
				snap.Box.HomePeriods.Add(started ? Math.Max(0, h1 - h0) : (-2));
				snap.Box.AwayPeriods.Add(started ? Math.Max(0, a1 - a0) : (-2));
				if (started)
				{
					hs += Math.Max(0, h1 - h0);
					aws += Math.Max(0, a1 - a0);
				}
				boundary += ((num < lengths.Count) ? lengths[num] : 0.0);
			}
		}
		else if (sport == Sport.Basketball)
		{
			for (int num2 = 0; num2 < periods; num2++)
			{
				double share;
				if (num2 < current)
				{
					share = 1.0;
				}
				else if (num2 != current)
				{
					share = 0.0;
				}
				else
				{
					share = ((lengths[num2] <= 0.0) ? 0.0 : (into / lengths[num2]));
				}
				if (f >= 1.0)
				{
					share = 1.0;
				}
				int h2 = ((num2 < final.HomePeriods.Count) ? ((int)Math.Floor((double)final.HomePeriods[num2] * share)) : 0);
				int a2 = ((num2 < final.AwayPeriods.Count) ? ((int)Math.Floor((double)final.AwayPeriods[num2] * share)) : 0);
				bool started2 = num2 <= current || f >= 1.0;
				snap.Box.HomePeriods.Add(started2 ? h2 : (-2));
				snap.Box.AwayPeriods.Add(started2 ? a2 : (-2));
				hs += h2;
				aws += a2;
			}
		}
		else
		{
			int[] hp = new int[periods];
			int[] ap = new int[periods];
			foreach (BoxEvent e3 in snap.Box.Events)
			{
				if (e3.Kind != 0)
				{
					continue;
				}
				int p2 = ((sport == Sport.Baseball) ? ((e3.Period - 1) / 2) : (e3.Period - 1));
				if (p2 >= 0 && p2 < periods)
				{
					int points = Math.Max(1, e3.Home ? (e3.HomeScore - hs) : (e3.AwayScore - aws));
					if (e3.Home)
					{
						hp[p2] += points;
						hs = e3.HomeScore;
					}
					else
					{
						ap[p2] += points;
						aws = e3.AwayScore;
					}
				}
			}
			int currentPeriod = ((sport == Sport.Baseball) ? (current / 2) : current);
			for (int num3 = 0; num3 < periods; num3++)
			{
				bool started3 = num3 <= currentPeriod || f >= 1.0;
				bool homeStarted = started3 && (sport != Sport.Baseball || num3 != currentPeriod || current % 2 != 0 || !(f < 1.0));
				snap.Box.AwayPeriods.Add(started3 ? ap[num3] : (-2));
				List<int> homePeriods = snap.Box.HomePeriods;
				int item;
				if (homeStarted)
				{
					item = ((num3 < final.HomePeriods.Count && final.HomePeriods[num3] < 0 && f >= 1.0) ? (-1) : hp[num3]);
				}
				else
				{
					item = -2;
				}
				homePeriods.Add(item);
			}
			if (shootout && f >= 1.0)
			{
				snap.Box.HomePeriods[periods - 1] = final.HomePeriods[periods - 1];
				snap.Box.AwayPeriods[periods - 1] = final.AwayPeriods[periods - 1];
			}
		}
		if (f < 1.0)
		{
			int reached = ((sport == Sport.Baseball) ? (current / 2 + 1) : (current + 1));
			int visible = Math.Max(Math.Min(periods, Timeline.Regulation(sport)), reached);
			while (snap.Box.HomePeriods.Count > visible)
			{
				snap.Box.HomePeriods.RemoveAt(snap.Box.HomePeriods.Count - 1);
			}
			while (snap.Box.AwayPeriods.Count > visible)
			{
				snap.Box.AwayPeriods.RemoveAt(snap.Box.AwayPeriods.Count - 1);
			}
		}
		snap.HomeScore = ((f >= 1.0) ? live.HomeScore : hs);
		snap.AwayScore = ((f >= 1.0) ? live.AwayScore : aws);
		if (f >= 1.0)
		{
			snap.Box.Note = final.Note;
		}
		double homeShare = ((live.HomeScore <= 0) ? f : Math.Min(1.0, (double)snap.HomeScore / (double)live.HomeScore));
		double awayShare = ((live.AwayScore <= 0) ? f : Math.Min(1.0, (double)snap.AwayScore / (double)live.AwayScore));
		int count = final.Lines.Count;
		int len = Stats.Length(sport);
		List<int[]> finals = new List<int[]>();
		List<int[]> sofar = new List<int[]>();
		foreach (BoxLine line in final.Lines)
		{
			finals.Add(Stats.Fit(line.Stats, sport));
			sofar.Add(new int[len]);
		}
		bool[] array = new bool[2] { true, false };
		foreach (bool side in array)
		{
			for (int num5 = 0; num5 < len; num5++)
			{
				List<int> idx = new List<int>();
				List<double> exact = new List<double>();
				double share2 = StatShare(sport, num5, side, f, fp, side ? homeShare : awayShare);
				for (int num6 = 0; num6 < count; num6++)
				{
					if (final.Lines[num6].Home == side)
					{
						idx.Add(num6);
						exact.Add((double)finals[num6][num5] * share2);
					}
				}
				int[] parts = Apportion(exact);
				for (int num7 = 0; num7 < idx.Count; num7++)
				{
					int st = finals[idx[num7]][num5];
					sofar[idx[num7]][num5] = ((st >= 0) ? Math.Max(0, Math.Min(st, parts[num7])) : Math.Min(0, Math.Max(st, parts[num7])));
				}
			}
		}
		for (int num8 = 0; num8 < count; num8++)
		{
			BoxLine line2 = final.Lines[num8];
			int[] st2 = finals[num8];
			int[] so = sofar[num8];
			switch (sport)
			{
			case Sport.Hockey:
				HockeySoFar(so, st2, line2, snap.Box.Events, f);
				break;
			case Sport.Basketball:
				BasketballSoFar(so, st2, f);
				break;
			case Sport.Football:
				FootballSoFar(so, st2, line2, snap.Box.Events, f);
				break;
			case Sport.Baseball:
				BaseballSoFar(so, st2, line2, snap.Box.Events, f);
				break;
			default:
				SoccerSoFar(so, st2, line2, snap.Box.Events, f);
				break;
			}
			snap.Box.Lines.Add(new BoxLine
			{
				PlayerId = line2.PlayerId,
				Home = line2.Home,
				Stats = so
			});
		}
		if (f < 1.0)
		{
			if (sport == Sport.Hockey || sport == Sport.Soccer)
			{
				ShotsSoFar(sport, final, snap.Box, f);
			}
			if (sport == Sport.Football)
			{
				PassingSoFar(final, snap.Box);
			}
		}
		if (sport == Sport.Baseball)
		{
			PitchersSoFar(final, snap.Box, f);
		}
		if (now < live.Start && g.RainDelay > 0)
		{
			ref string status = ref snap.Status;
			string text;
			if (WeatherCodes.Kind(g.Weather) == WeatherKind.Storm)
			{
				text = "Lightning delay";
			}
			else
			{
				text = ((WeatherCodes.Kind(g.Weather) == WeatherKind.Snow) ? "Snow delay" : "Rain delay");
			}
			status = text + ". Start pushed back to " + GameClock.ClockLabel(GameClock.MinuteOfDay(live.Start));
		}
		return snap;
	}

	private static FootballProgress FootballDrives(List<int> drives, double elapsed, bool over, LiveSnapshot snap)
	{
		FootballProgress fp = new FootballProgress();
		int n = 8;
		double homePlays = 0.0;
		double homeYards = 0.0;
		double awayPlays = 0.0;
		double awayYards = 0.0;
		double hp = 0.0;
		double hy = 0.0;
		double ap = 0.0;
		double ay = 0.0;
		for (int i = 0; i + n <= drives.Count; i += n)
		{
			bool home = drives[i] == 1;
			int start = drives[i + 2];
			int end = drives[i + 3];
			int plays = drives[i + 4];
			int yards = drives[i + 5];
			int result = drives[i + 6];
			if (home)
			{
				homePlays += (double)plays;
				homeYards += (double)Math.Max(0, yards);
				if (result == 4)
				{
					fp.HomeIntsTotal++;
				}
			}
			else
			{
				awayPlays += (double)plays;
				awayYards += (double)Math.Max(0, yards);
				if (result == 4)
				{
					fp.AwayIntsTotal++;
				}
			}
			if (over || (double)end <= elapsed)
			{
				if (home)
				{
					hp += (double)plays;
					hy += (double)Math.Max(0, yards);
					if (result == 4)
					{
						fp.HomeInts++;
					}
				}
				else
				{
					ap += (double)plays;
					ay += (double)Math.Max(0, yards);
					if (result == 4)
					{
						fp.AwayInts++;
					}
				}
				for (int k = 0; k < n; k++)
				{
					snap.Box.Flow.Add(drives[i + k]);
				}
			}
			else if (!((double)start > elapsed))
			{
				double share = ((end <= start) ? 0.0 : Math.Max(0.0, Math.Min(1.0, (elapsed - (double)start) / (double)(end - start))));
				int playsSoFar = (int)Math.Floor((double)plays * share);
				int yardsSoFar = (int)Math.Round((double)yards * share);
				if (home)
				{
					hp += (double)playsSoFar;
					hy += (double)Math.Max(0, yardsSoFar);
				}
				else
				{
					ap += (double)playsSoFar;
					ay += (double)Math.Max(0, yardsSoFar);
				}
				snap.Box.Flow.AddRange(new int[8]
				{
					drives[i],
					drives[i + 1],
					start,
					(int)elapsed,
					playsSoFar,
					yardsSoFar,
					-1,
					drives[i + 7]
				});
				snap.DriveHome = (home ? 1 : 0);
				snap.DrivePlays = playsSoFar;
				snap.DriveYards = yardsSoFar;
				snap.DriveYard = Math.Max(1, Math.Min(99, drives[i + 7] + yardsSoFar));
			}
		}
		fp.HomePlays = ((homePlays <= 0.0) ? 1.0 : Math.Min(1.0, hp / homePlays));
		fp.HomeYards = ((homeYards <= 0.0) ? fp.HomePlays : Math.Min(1.0, hy / homeYards));
		fp.AwayPlays = ((awayPlays <= 0.0) ? 1.0 : Math.Min(1.0, ap / awayPlays));
		fp.AwayYards = ((awayYards <= 0.0) ? fp.AwayPlays : Math.Min(1.0, ay / awayYards));
		return fp;
	}

	private static void ScoreAt(List<int> track, double seconds, out int home, out int away)
	{
		int minutes = track.Count / 2;
		int k = (int)Math.Floor(seconds / 60.0);
		double frac = seconds / 60.0 - (double)k;
		int h0 = 0;
		int a0 = 0;
		if (k >= 1 && k - 1 < minutes)
		{
			h0 = track[2 * (k - 1)];
			a0 = track[2 * (k - 1) + 1];
		}
		else if (k - 1 >= minutes && minutes > 0)
		{
			h0 = track[2 * (minutes - 1)];
			a0 = track[2 * (minutes - 1) + 1];
		}
		int h1 = h0;
		int a1 = a0;
		if (k < minutes)
		{
			h1 = track[2 * k];
			a1 = track[2 * k + 1];
		}
		home = h0 + (int)Math.Floor((double)(h1 - h0) * frac);
		away = a0 + (int)Math.Floor((double)(a1 - a0) * frac);
	}

	private static double PeriodLength(Sport sport, Level level, int period, int periods, bool shootout, bool playoff)
	{
		switch (sport)
		{
		case Sport.Hockey:
			if (period <= 3)
			{
				return 1200.0;
			}
			if (shootout && period == periods)
			{
				return 180.0;
			}
			return playoff ? 1200 : 300;
		case Sport.Basketball:
		{
			double q = SportRules.Scale(level) switch
			{
				Level.College => 600, 
				Level.Pro => 720, 
				_ => 480, 
			};
			if (period > 4)
			{
				return 300.0;
			}
			return q;
		}
		case Sport.Football:
			return (period <= 4) ? 900 : 600;
		case Sport.Soccer:
			return 2880.0;
		default:
			return 1.0;
		}
	}

	private static double EventSeconds(Sport sport, BoxEvent e)
	{
		switch (sport)
		{
		case Sport.Soccer:
		{
			string c = e.Clock.TrimEnd('\'');
			int plus = c.IndexOf('+');
			int added = 0;
			int minute;
			if (plus > 0)
			{
				int.TryParse(c.Substring(0, plus), out minute);
				int.TryParse(c.Substring(plus + 1), out added);
			}
			else
			{
				int.TryParse(c, out minute);
			}
			int inHalf = ((e.Period == 2) ? (minute - 45) : minute);
			return Math.Max(0, inHalf * 60 + added * 30);
		}
		case Sport.Football:
			return Math.Max(0, ((e.Period > 4) ? 600 : 900) - ClockSeconds(e.Clock));
		default:
			return ClockSeconds(e.Clock);
		}
	}

	private static int ClockSeconds(string clock)
	{
		int colon = clock.IndexOf(':');
		if (colon <= 0 || !int.TryParse(clock.Substring(0, colon), out var m) || !int.TryParse(clock.Substring(colon + 1), out var sec))
		{
			return 0;
		}
		return m * 60 + sec;
	}

	private static string StatusText(Sport sport, Level level, int period, double into, double length, int periods, bool shootout)
	{
		string clock = Timeline.MinSec((int)Math.Max(0.0, Math.Round(length - into))) + " left";
		switch (sport)
		{
		case Sport.Hockey:
		{
			if (shootout && period == periods)
			{
				return "Shootout";
			}
			string text2;
			if (period <= 3)
			{
				text2 = Timeline.Ordinal(period) + " period";
			}
			else
			{
				text2 = ((period == 4) ? "Overtime" : (Timeline.Ordinal(period - 3) + " overtime"));
			}
			return text2 + ", " + clock;
		}
		case Sport.Basketball:
		case Sport.Football:
		{
			string text;
			if (period <= 4)
			{
				text = Timeline.Ordinal(period) + " quarter";
			}
			else
			{
				text = ((period == 5) ? "Overtime" : (Timeline.Ordinal(period - 4) + " overtime"));
			}
			return text + ", " + clock;
		}
		case Sport.Baseball:
			return ((period % 2 == 1) ? "Top " : "Bottom ") + Timeline.Ordinal((period + 1) / 2);
		default:
		{
			int minute = (int)(into / 60.0) + ((period == 2) ? 45 : 0) + 1;
			int cap = ((period == 2) ? 90 : 45);
			if (minute <= cap)
			{
				return minute + "'";
			}
			return cap + "+" + (minute - cap) + "'";
		}
		}
	}

	private static int Scored(List<BoxEvent> events, int playerId, int index)
	{
		int n = 0;
		foreach (BoxEvent e in events)
		{
			if (e.Kind != 0 || e.Players == null)
			{
				continue;
			}
			if (index == 0 && e.Players.Count > 0 && e.Players[0] == playerId)
			{
				n++;
			}
			if (index <= 0)
			{
				continue;
			}
			for (int i = 1; i < e.Players.Count; i++)
			{
				if (e.Players[i] == playerId)
				{
					n++;
				}
			}
		}
		return n;
	}

	private static double StatShare(Sport sport, int k, bool home, double f, FootballProgress fp, double scoreShare)
	{
		if (f >= 1.0)
		{
			return 1.0;
		}
		switch (sport)
		{
		case Sport.Football:
			if (k == 0)
			{
				return 1.0;
			}
			if (fp == null)
			{
				return f;
			}
			switch (k)
			{
			case 1:
			case 2:
			case 6:
			case 9:
				if (!home)
				{
					return fp.AwayPlays;
				}
				return fp.HomePlays;
			case 3:
			case 7:
			case 10:
				if (!home)
				{
					return fp.AwayYards;
				}
				return fp.HomeYards;
			case 12:
			case 13:
				if (!home)
				{
					return fp.HomePlays;
				}
				return fp.AwayPlays;
			case 5:
			{
				int t2 = (home ? fp.HomeIntsTotal : fp.AwayIntsTotal);
				if (t2 > 0)
				{
					return (double)(home ? fp.HomeInts : fp.AwayInts) / (double)t2;
				}
				return 0.0;
			}
			case 14:
			{
				int t = (home ? fp.AwayIntsTotal : fp.HomeIntsTotal);
				if (t > 0)
				{
					return (double)(home ? fp.AwayInts : fp.HomeInts) / (double)t;
				}
				return 0.0;
			}
			default:
				return f;
			}
		case Sport.Basketball:
			switch (k)
			{
			case 0:
			case 12:
				return 1.0;
			case 1:
			case 6:
			case 7:
			case 8:
			case 9:
			case 10:
				return scoreShare;
			default:
				return f;
			}
		case Sport.Hockey:
			if (k != 0 && k != 11)
			{
				return f;
			}
			return 1.0;
		case Sport.Baseball:
			if (k != 0 && k != 10 && k != 9)
			{
				return f;
			}
			return 1.0;
		default:
			if (k != 0 && k != 8)
			{
				return f;
			}
			return 1.0;
		}
	}

	private static int[] Apportion(List<double> exact)
	{
		int n = exact.Count;
		int[] parts = new int[n];
		if (n == 0)
		{
			return parts;
		}
		double sum = 0.0;
		foreach (double e in exact)
		{
			sum += e;
		}
		int target = ((sum >= 0.0) ? ((int)Math.Floor(sum + 1E-06)) : ((int)Math.Ceiling(sum - 1E-06)));
		int given = 0;
		for (int i = 0; i < n; i++)
		{
			parts[i] = (int)Math.Floor(exact[i] + 1E-09);
			given += parts[i];
		}
		List<int> order = new List<int>();
		for (int j = 0; j < n; j++)
		{
			order.Add(j);
		}
		order.Sort((int a, int b) => (exact[b] - (double)parts[b]).CompareTo(exact[a] - (double)parts[a]));
		int k = 0;
		while (given < target && k < 4 * n)
		{
			parts[order[k % n]]++;
			given++;
			k++;
		}
		int k2 = n - 1;
		while (given > target && k2 >= -3 * n)
		{
			parts[order[(k2 % n + n) % n]]--;
			given--;
			k2--;
		}
		return parts;
	}

	private static void HockeySoFar(int[] so, int[] st, BoxLine line, List<BoxEvent> events, double f)
	{
		so[0] = st[0];
		so[11] = st[11];
		so[1] = Math.Min(st[1], Scored(events, line.PlayerId, 0));
		so[2] = Math.Min(st[2], Scored(events, line.PlayerId, 1));
		if (f < 1.0)
		{
			so[5] = (so[6] = (so[7] = (so[10] = 0)));
		}
	}

	private static void ShotsSoFar(Sport sport, GameBox final, GameBox now, double f)
	{
		bool hockey = sport == Sport.Hockey;
		int shotStat = (hockey ? 3 : 3);
		int goalStat = (hockey ? 1 : 1);
		int[] teamShots = new int[2];
		int[] teamGoals = new int[2];
		int[] emptyNet = new int[2];
		foreach (BoxEvent e in now.Events)
		{
			if (e.Kind == 0)
			{
				int t = (e.Home ? 1 : 0);
				teamGoals[t]++;
				if (hockey && e.Text.EndsWith("  EN"))
				{
					emptyNet[t]++;
				}
			}
		}
		bool[] array = new bool[2] { true, false };
		foreach (bool side in array)
		{
			List<int> idx = new List<int>();
			List<double> exact = new List<double>();
			for (int j = 0; j < final.Lines.Count; j++)
			{
				if (final.Lines[j].Home == side)
				{
					int[] st = Stats.Fit(final.Lines[j].Stats, sport);
					if (!IsKeeper(sport, st))
					{
						idx.Add(j);
						exact.Add((double)Math.Max(0, st[shotStat] - st[goalStat]) * f);
					}
				}
			}
			int[] parts = Apportion(exact);
			for (int n = 0; n < idx.Count; n++)
			{
				int[] st2 = Stats.Fit(final.Lines[idx[n]].Stats, sport);
				int[] so = now.Lines[idx[n]].Stats;
				so[shotStat] = Math.Min(st2[shotStat], so[goalStat] + Math.Max(0, parts[n]));
				teamShots[side ? 1u : 0u] += so[shotStat];
			}
		}
		for (int k = 0; k < final.Lines.Count; k++)
		{
			int[] st3 = Stats.Fit(final.Lines[k].Stats, sport);
			int[] so2 = now.Lines[k].Stats;
			if (IsKeeper(sport, st3))
			{
				int opp = ((!final.Lines[k].Home) ? 1 : 0);
				int against = teamGoals[opp] - emptyNet[opp];
				if (hockey)
				{
					so2[9] = Math.Min(st3[9], against);
					so2[8] = Math.Max(so2[9], Math.Min(st3[8], teamShots[opp] - emptyNet[opp]));
				}
				else
				{
					so2[6] = Math.Min(st3[6], against);
					so2[5] = Math.Max(0, Math.Min(Math.Min(st3[5], (int)Math.Floor((double)st3[5] * f + 1E-09)), teamShots[opp] - teamGoals[opp]));
					so2[7] = 0;
				}
			}
		}
	}

	private static bool IsKeeper(Sport sport, int[] st)
	{
		if (sport != Sport.Hockey)
		{
			if (st[8] <= 0 && st[5] <= 0)
			{
				return st[6] > 0;
			}
			return true;
		}
		if (st[11] <= 0)
		{
			return st[8] > 0;
		}
		return true;
	}

	private static void BasketballSoFar(int[] so, int[] st, double f)
	{
		so[0] = st[0];
		so[12] = st[12];
		if (st[11] > 0 && f > 0.0)
		{
			so[11] = Math.Max(1, (int)Math.Ceiling((double)st[11] * f));
		}
		so[8] = Math.Min(so[8], so[6]);
		so[7] = Math.Max(so[7], so[6]);
		so[10] = Math.Max(so[10], so[9]);
	}

	private static void PassingSoFar(GameBox final, GameBox now)
	{
		bool[] array = new bool[2] { true, false };
		foreach (bool side in array)
		{
			int rec = 0;
			int recYds = 0;
			int passer = -1;
			int mostAtt = -1;
			int othersCmp = 0;
			int othersYds = 0;
			for (int j = 0; j < now.Lines.Count; j++)
			{
				if (now.Lines[j].Home == side)
				{
					int[] so = now.Lines[j].Stats;
					int[] st = Stats.Fit(final.Lines[j].Stats, Sport.Football);
					rec += so[9];
					recYds += so[10];
					if (st[2] > mostAtt)
					{
						mostAtt = st[2];
						passer = j;
					}
				}
			}
			if (passer < 0 || mostAtt <= 0)
			{
				continue;
			}
			for (int k = 0; k < now.Lines.Count; k++)
			{
				if (now.Lines[k].Home == side && k != passer)
				{
					othersCmp += now.Lines[k].Stats[1];
					othersYds += now.Lines[k].Stats[3];
				}
			}
			int[] p = now.Lines[passer].Stats;
			p[1] = Math.Max(0, rec - othersCmp);
			p[3] = recYds - othersYds;
			p[2] = Math.Max(p[2], p[1]);
		}
	}

	private static void FootballSoFar(int[] so, int[] st, BoxLine line, List<BoxEvent> events, double f)
	{
		so[0] = st[0];
		int rec = 0;
		int rush = 0;
		int passed = 0;
		int fg = 0;
		foreach (BoxEvent e in events)
		{
			if (e.Kind != 0 || e.Players == null || e.Players.Count == 0)
			{
				continue;
			}
			if (e.Text.Contains("field goal"))
			{
				if (e.Players[0] == line.PlayerId)
				{
					fg++;
				}
			}
			else if (e.Players.Count > 1)
			{
				if (e.Players[0] == line.PlayerId)
				{
					rec++;
				}
				if (e.Players[1] == line.PlayerId)
				{
					passed++;
				}
			}
			else if (e.Players[0] == line.PlayerId)
			{
				rush++;
			}
		}
		so[11] = Math.Min(st[11], rec);
		so[8] = Math.Min(st[8], rush);
		so[4] = Math.Min(st[4], passed);
		so[15] = Math.Min(st[15], fg);
		so[16] = Math.Max(so[15], so[16]);
		so[9] = Math.Max(so[11], so[9]);
		so[1] = Math.Max(so[4], so[1]);
		so[2] = Math.Max(so[1], so[2]);
		if (st[18] <= 0)
		{
			return;
		}
		int made = 0;
		int missed = 0;
		foreach (BoxEvent e2 in events)
		{
			if (e2.Kind == 0 && e2.Home == line.Home)
			{
				if (e2.Text.EndsWith("(kick failed)"))
				{
					missed++;
				}
				else if (e2.Text.EndsWith(" kick)") || e2.Text.EndsWith("(kick)"))
				{
					made++;
				}
			}
		}
		so[17] = Math.Min(st[17], made);
		so[18] = Math.Min(st[18], made + missed);
	}

	private static void BaseballSoFar(int[] so, int[] st, BoxLine line, List<BoxEvent> events, double f)
	{
		so[0] = st[0];
		so[10] = st[10];
		so[9] = st[9];
		int rbi = 0;
		int hr = 0;
		foreach (BoxEvent e in events)
		{
			if (e.Kind == 0 && e.Players != null && e.Players.Count != 0 && e.Players[0] == line.PlayerId)
			{
				rbi += Math.Max(1, RunsOf(e, events));
				if (e.Text.Contains("home run") || e.Text.Contains("grand slam"))
				{
					hr++;
				}
			}
		}
		so[6] = Math.Min(st[6], rbi);
		so[5] = Math.Min(st[5], hr);
		so[3] = Math.Max(so[5], so[3]);
		so[1] = Math.Max(so[3], so[1]);
		if (f < 1.0)
		{
			so[16] = (so[17] = (so[18] = 0));
			return;
		}
		so[16] = st[16];
		so[17] = st[17];
		so[18] = st[18];
	}

	private static void PitchersSoFar(GameBox final, GameBox now, double f)
	{
		bool[] array = new bool[2] { false, true };
		foreach (bool home in array)
		{
			List<int> order = new List<int>();
			int total = 0;
			for (int j = 0; j < final.Lines.Count; j++)
			{
				BoxLine boxLine = final.Lines[j];
				int[] st = Stats.Fit(boxLine.Stats, Sport.Baseball);
				if (boxLine.Home == home && st[9] > 0)
				{
					total += st[11];
					if (st[10] > 0)
					{
						order.Insert(0, j);
					}
					else
					{
						order.Add(j);
					}
				}
			}
			int left = (int)Math.Floor((double)total * f);
			foreach (int i2 in order)
			{
				int[] st2 = Stats.Fit(final.Lines[i2].Stats, Sport.Baseball);
				int[] so = now.Lines[i2].Stats;
				int outs = Math.Min(st2[11], left);
				left -= outs;
				bool pitching = outs > 0 || IsNextUp(order, final, i2, total, f);
				double share = ((st2[11] <= 0) ? f : ((double)outs / (double)st2[11]));
				so[11] = outs;
				int[] array2 = new int[4] { 12, 14, 15, 13 };
				foreach (int k2 in array2)
				{
					so[k2] = (int)Math.Floor((double)st2[k2] * share);
				}
				so[9] = ((pitching || st2[10] > 0) ? st2[9] : 0);
				if (f >= 1.0)
				{
					Array.Copy(st2, so, Math.Min(st2.Length, so.Length));
				}
			}
		}
	}

	private static bool IsNextUp(List<int> order, GameBox final, int index, int total, double f)
	{
		int left = (int)Math.Floor((double)total * f);
		foreach (int i in order)
		{
			int outs = Stats.Fit(final.Lines[i].Stats, Sport.Baseball)[11];
			if (left < outs)
			{
				return i == index;
			}
			left -= outs;
		}
		return false;
	}

	private static int RunsOf(BoxEvent e, List<BoxEvent> events)
	{
		int num = events.IndexOf(e);
		int prevHome = 0;
		int prevAway = 0;
		for (int k = num - 1; k >= 0; k--)
		{
			if (events[k].Kind == 0)
			{
				prevHome = events[k].HomeScore;
				prevAway = events[k].AwayScore;
				break;
			}
		}
		if (!e.Home)
		{
			return e.AwayScore - prevAway;
		}
		return e.HomeScore - prevHome;
	}

	private static void SoccerSoFar(int[] so, int[] st, BoxLine line, List<BoxEvent> events, double f)
	{
		so[0] = st[0];
		so[8] = st[8];
		so[1] = Scored(events, line.PlayerId, 0);
		so[2] = Scored(events, line.PlayerId, 1);
		so[3] = Math.Max(so[1], so[3]);
		int cards = 0;
		foreach (BoxEvent e in events)
		{
			if (e.Kind == 1 && e.Text.StartsWith("Yellow") && e.Players != null && e.Players.Count > 0 && e.Players[0] == line.PlayerId)
			{
				cards++;
			}
		}
		so[4] = cards;
		int reds = 0;
		foreach (BoxEvent e2 in events)
		{
			if (e2.Kind == 1 && e2.Text.StartsWith("Red") && e2.Players != null && e2.Players.Count > 0 && e2.Players[0] == line.PlayerId)
			{
				reds++;
			}
		}
		if (so.Length > 9)
		{
			so[9] = reds;
		}
		if (st[8] <= 0 && st[5] <= 0 && st[6] <= 0)
		{
			return;
		}
		int against = 0;
		foreach (BoxEvent e3 in events)
		{
			if (e3.Kind == 0 && e3.Home != line.Home)
			{
				against++;
			}
		}
		so[6] = Math.Min(st[6], against);
		so[7] = ((f >= 1.0) ? st[7] : 0);
	}

	private static List<KeyValuePair<string, string>> OfficeTabs(Team t)
	{
		List<KeyValuePair<string, string>> tabs = new List<KeyValuePair<string, string>>
		{
			new KeyValuePair<string, string>("", "Overview")
		};
		if (SportRules.ProLike(t.Sport, t.Level))
		{
			tabs.Add(new KeyValuePair<string, string>("contracts", "Contracts"));
			tabs.Add(new KeyValuePair<string, string>("fa", "Free agents"));
			tabs.Add((t.Sport == Sport.Soccer) ? new KeyValuePair<string, string>("transfers", "Transfers") : new KeyValuePair<string, string>("trade", "Trades"));
			if (t.Level == Level.Pro && SportsWorld.HasDraft(t.Sport))
			{
				tabs.Add(new KeyValuePair<string, string>("draft", "Draft"));
			}
		}
		if (t.Level == Level.Pro && SportRules.HasMinors(t.Sport))
		{
			tabs.Add(new KeyValuePair<string, string>("farm", "Farm team"));
		}
		if (t.Level == Level.College)
		{
			tabs.Add(new KeyValuePair<string, string>("recruiting", "Recruiting"));
		}
		tabs.Add(new KeyValuePair<string, string>("staff", "Coaches"));
		return tabs;
	}

	private static void WriteFarm(JsonBuilder j, SportsWorld world, Team t, League league, int now)
	{
		Team farm = world.AffiliateOf(t);
		int year = GameClock.YearOfDay(GameClock.DayOf(now));
		j.Name("farm").BeginObject();
		j.Prop("has", farm != null);
		j.Prop("pref", t.FarmPref);
		j.Prop("prefNote", "Automatic: your city's Minor League team if you have one, otherwise a club in another city. In your city: one of your Minor League teams. Out of town: a club in another city, even if you have a team here (your team then becomes another club's farm team).");
		if (farm == null)
		{
			j.Prop("note", "The " + world.TeamName(t.Id) + " don't have a farm team right now. Make a building a venue, pick Minor League as its level and add a " + SportRules.SportLabel(t.Sport).ToLowerInvariant() + " team, or choose Out of town above.");
			j.EndObject();
			return;
		}
		League minors = world.LeagueOf(farm);
		string record = "";
		if (minors != null && minors.Current != null && minors.TeamIds.Contains(farm.Id))
		{
			foreach (StandingRow r in Standings.Compute(minors, minors.Current))
			{
				if (r.TeamId == farm.Id)
				{
					record = Standings.RecordText(farm.Sport, r);
				}
			}
		}
		j.Prop("teamId", farm.Id);
		j.Prop("name", world.TeamName(farm.Id));
		j.Prop("record", record);
		j.Prop("isUser", farm.IsUser);
		j.Prop("where", farm.IsUser ? "In your city" : "Out of town");
		j.Prop("manual", t.Manual);
		j.Prop("note", t.Manual ? ("Call a player up and he signs a minimum deal (two seasons if he's 24 or younger). Players " + 25 + " and under on cheap deals can be sent down to play and called up again later; they don't count against your " + Economy.Get(t.Sport).CapName.ToLowerInvariant() + " while they're down.") : "The AI general manager calls players up and sends young players down. Choose \"Run this team myself\" on the Overview tab to do it yourself.");
		j.Name("players").BeginArray();
		List<Player> list = world.RosterOf(farm.Id);
		list.Sort((Player a, Player b) => (b.Overall == a.Overall) ? a.Id.CompareTo(b.Id) : b.Overall.CompareTo(a.Overall));
		foreach (Player p in list)
		{
			string block = world.CallUpBlock(t, p);
			j.BeginObject();
			WritePlayerBasics(j, world, farm, p, year);
			j.Prop("line", Stats.IsEmpty(p.Season) ? "" : AwardBook.SportLine(p.Sport, p, Stats.Fit(p.Season, p.Sport)));
			j.Prop("can", t.Manual && block == null);
			j.Prop("why", block ?? "");
			j.EndObject();
		}
		j.EndArray();
		j.Name("down").BeginArray();
		List<Player> list2 = world.SignedPlayers(t.Id);
		list2.Sort((Player a, Player b) => (b.Overall == a.Overall) ? a.Id.CompareTo(b.Id) : b.Overall.CompareTo(a.Overall));
		foreach (Player p2 in list2)
		{
			if (world.SendDownBlock(t, p2) == null)
			{
				j.BeginObject();
				WritePlayerBasics(j, world, t, p2, year);
				j.Prop("can", t.Manual);
				j.Prop("why", "");
				j.EndObject();
			}
		}
		j.EndArray();
		j.EndObject();
	}

	public static int OfficeAlerts(SportsWorld world, Team t, int now)
	{
		int n = 0;
		League league = world.LeagueOf(t);
		if (league == null)
		{
			return 0;
		}
		if (t.Manual)
		{
			if (t.Level == Level.Pro && world.WaitingOnUserPick(league) == t)
			{
				n++;
			}
			foreach (TradeOffer offer in world.Offers)
			{
				if (offer.ToTeamId == t.Id)
				{
					n++;
				}
			}
			foreach (TransferOffer transferOffer in world.TransferOffers)
			{
				if (transferOffer.SellerTeamId == t.Id)
				{
					n++;
				}
			}
			if (world.CoachingWindowOpen(league, now) && world.CoachOf(t.Id, CoachRole.Head) == null)
			{
				n++;
			}
		}
		return n;
	}

	private static void WriteOverview(JsonBuilder j, SportsWorld world, Team t, League league, int now)
	{
		bool pro = SportRules.ProLike(t.Sport, t.Level);
		List<OfficeCard> cards = new List<OfficeCard>();
		world.CurrentYear();
		int today = GameClock.DayOf(now);
		Func<string, string, string, OfficeCard> card = (string title, string tab, string button) =>
		{
			OfficeCard officeCard = new OfficeCard
			{
				Title = title,
				Tab = tab,
				Button = button
			};
			cards.Add(officeCard);
			return officeCard;
		};
		List<CalendarEvent> calendar = ((league == null) ? new List<CalendarEvent>() : world.Calendar(league, now));
		Func<string, string> nextDate = (string title) =>
		{
			foreach (CalendarEvent current in calendar)
			{
				if ((current.Title == title || current.Title.EndsWith(" " + title)) && current.State != 0)
				{
					return current.Date;
				}
			}
			return "";
		};
		if (pro)
		{
			List<Player> signed = world.SignedPlayers(t.Id);
			OfficeCard cap = card("Salary cap", "contracts", "Contracts");
			cap.Lines.Add(CapLine(world, t));
			int dead = Economy.DeadNow(t);
			if (dead > 0)
			{
				cap.Lines.Add("Dead money this season: " + Economy.Money(dead) + ((Economy.DeadLater(t) > 0) ? (", " + Economy.Money(Economy.DeadLater(t)) + " still to come") : ""));
			}
			cap.Lines.Add(signed.Count + " players under contract");
			int expiring = signed.FindAll((Player player) => player.ContractYears <= 1).Count;
			OfficeCard contracts = card("Contracts", "contracts", "Talk contracts");
			contracts.Lines.Add((expiring == 0) ? "Nobody's deal ends after this season." : (expiring + ((expiring == 1) ? " player's deal ends" : " players' deals end") + " after this season. Extend them before free agency opens."));
			string fa = nextDate("Free agency opens");
			if (fa.Length > 0)
			{
				contracts.Lines.Add("Free agency opens " + fa);
			}
			OfficeCard market = card("Free agents", "fa", "Free agents");
			List<Player> fas = new List<Player>(world.FreeAgents(t.Sport));
			fas.Sort((Player a, Player b) => b.Overall.CompareTo(a.Overall));
			if (fas.Count == 0)
			{
				market.Lines.Add("Nobody on the market right now.");
			}
			else
			{
				List<string> top = new List<string>();
				for (int i = 0; i < fas.Count && i < 3; i++)
				{
					top.Add(Rosters.PositionLabel(fas[i].Position) + " " + fas[i].Name + " (" + Rosters.Ovr(fas[i]) + ")");
				}
				market.Lines.Add(fas.Count + " on the market. Best: " + string.Join(", ", top.ToArray()));
			}
			if (t.Sport == Sport.Soccer)
			{
				OfficeCard tr = card("Transfers", "transfers", "Transfers");
				tr.Lines.Add("Transfer budget: " + Economy.Money(world.TransferBudgetOf(t)));
				tr.Lines.Add(SportsWorld.TransferWindowText(now));
				int bids = 0;
				foreach (TransferOffer transferOffer in world.TransferOffers)
				{
					if (transferOffer.SellerTeamId == t.Id)
					{
						bids++;
					}
				}
				tr.Lines.Add((t.Block.Count == 0) ? "Nobody on your transfer list." : (t.Block.Count + ((t.Block.Count == 1) ? " player" : " players") + " on your transfer list."));
				if (bids > 0)
				{
					tr.Lines.Add(bids + ((bids == 1) ? " offer" : " offers") + " for your players.");
					tr.Urgent = true;
				}
			}
			OfficeCard trades = ((t.Sport == Sport.Soccer) ? new OfficeCard() : card("Trades", "trade", "Trades"));
			int calls = 0;
			foreach (TradeOffer offer in world.Offers)
			{
				if (offer.ToTeamId == t.Id)
				{
					calls++;
				}
			}
			trades.Lines.Add((t.Block.Count == 0) ? "Nobody on your trade block." : (t.Block.Count + ((t.Block.Count == 1) ? " player" : " players") + " on your trade block."));
			if (calls > 0)
			{
				trades.Lines.Add(calls + ((calls == 1) ? " team called" : " teams called") + " about your block.");
				trades.Urgent = true;
			}
			string deadline = nextDate("Trade deadline");
			if (deadline.Length > 0)
			{
				trades.Lines.Add("Trade deadline " + deadline);
			}
			bool drafts = t.Level == Level.Pro && SportsWorld.HasDraft(t.Sport);
			OfficeCard draft = (drafts ? card("Draft", "draft", "Draft") : card("Young prospects", "fa", "Free agents"));
			if (!drafts)
			{
				List<Player> young = world.FreeAgents(t.Sport).FindAll(world.IsYoungProspect);
				young.Sort((Player a, Player b) => b.Potential.CompareTo(a.Potential));
				if (young.Count == 0)
				{
					draft.Lines.Add("No young prospects on the market right now. A new class arrives every " + Economy.Get(t.Sport).DraftDate(world.CurrentYear()).ToString("MMMM") + ".");
				}
				else
				{
					List<string> best = new List<string>();
					for (int i2 = 0; i2 < young.Count && i2 < 3; i2++)
					{
						best.Add(Rosters.PositionLabel(young[i2].Position) + " " + young[i2].Name + " (" + world.AgeOf(young[i2]) + ", POT " + Rosters.Grade(young[i2], world.AgeOf(young[i2])) + ")");
					}
					draft.Lines.Add(young.Count + " on the market. Best: " + string.Join(", ", best.ToArray()));
				}
				draft.Lines.Add("Your reserves: " + world.ProspectsOf(t.Id).Count + " of " + Economy.Get(t.Sport).MaxProspects + ".");
				if (t.Level != Level.Pro && league != null)
				{
					card("Promotion race", "#league", "Standings").Lines.Add(world.DivisionNote(league));
				}
			}
			else if (league != null && league.Draft != null && league.Draft.Started && !league.Draft.Complete)
			{
				draft.Lines.Add((world.WaitingOnUserPick(league) == t) ? "It's your pick!" : "The draft is on.");
				draft.Urgent = world.WaitingOnUserPick(league) == t;
			}
			else
			{
				string when = nextDate(Economy.Get(t.Sport).DraftName);
				draft.Lines.Add((when.Length > 0) ? ("Next " + Economy.Get(t.Sport).DraftName.ToLowerInvariant() + ": " + when) : "No draft coming up yet.");
			}
			if ((league != null) & drafts)
			{
				int picks = world.TradeablePicks(league, t.Id).Count;
				draft.Lines.Add("You own " + picks + " future draft " + ((picks == 1) ? "pick." : "picks."));
			}
		}
		if (t.Level == Level.College)
		{
			OfficeCard rec = card("Recruiting", "recruiting", "Recruiting");
			rec.Lines.Add("Prestige " + t.Prestige + " (" + SportsWorld.PrestigeLabel(t.Prestige) + ")");
			int offers = 0;
			int commits = 0;
			foreach (RecruitEffort e in world.RecruitEfforts)
			{
				if (e.TeamId == t.Id && e.Offered)
				{
					offers++;
				}
			}
			foreach (RecruitTarget item in world.RecruitBoard)
			{
				if (item.CommittedTo == t.Id)
				{
					commits++;
				}
			}
			rec.Lines.Add("Recruiting hours left this month: " + t.RecruitHours + " of " + world.RecruitHoursPerMonth(t));
			rec.Lines.Add(offers + ((offers == 1) ? " scholarship offered" : " scholarships offered") + ", " + commits + ((commits == 1) ? " commit" : " commits"));
		}
		Team farmTeam = world.AffiliateOf(t);
		Team parentClub = world.ParentOf(t);
		if (pro && SportRules.HasMinors(t.Sport))
		{
			OfficeCard f = card("Farm team", "farm", "Farm team");
			if (farmTeam == null)
			{
				f.Lines.Add("No farm team. Add a Minor League " + SportRules.SportLabel(t.Sport).ToLowerInvariant() + " team at one of your venues and it becomes this club's farm team.");
			}
			else
			{
				List<Player> farmers = world.RosterOf(farmTeam.Id);
				farmers.Sort((Player a, Player b) => b.Overall.CompareTo(a.Overall));
				f.Lines.Add(world.TeamName(farmTeam.Id) + (farmTeam.IsUser ? " (yours)" : ""));
				if (farmers.Count > 0)
				{
					f.Lines.Add("Best player: " + Rosters.PositionLabel(farmers[0].Position) + " " + farmers[0].Name + " (" + Rosters.Ovr(farmers[0]) + " OVR, " + world.AgeOf(farmers[0]) + ")");
				}
				int ready = farmers.FindAll((Player x) => world.CallUpBlock(t, x) == null && x.Overall >= Rosters.Base(Level.Pro) - 2).Count;
				f.Lines.Add((ready == 0) ? "Nobody ready for a call-up yet." : (ready + ((ready == 1) ? " player looks ready" : " players look ready") + " for a call-up."));
			}
		}
		if (t.Level == Level.Minor && !SportRules.IsSecondDivision(t.Sport, t.Level))
		{
			OfficeCard pc = card("Parent club", "#parent", "Open it");
			pc.Lines.Add((parentClub == null) ? "No pro club right now (there's no pro league in this sport yet). Add a Pro team and this becomes its farm team." : ("Farm team of the " + world.TeamName(parentClub.Id) + "."));
			if (parentClub != null)
			{
				pc.Lines.Add(parentClub.IsUser ? "Call players up and send them down in your pro club's front office (Farm team tab)." : "They call players up when they need them and send young players down to play.");
			}
		}
		OfficeCard staff = card("Coaches", "staff", "Coaches");
		Coach head = world.CoachOf(t.Id, CoachRole.Head);
		staff.Lines.Add((head == null) ? "No head coach right now." : ("Head coach: " + head.Name + " (overall " + head.Overall + ", " + CoachRecord(head) + ")"));
		bool window = league == null || world.CoachingWindowOpen(league, now);
		staff.Lines.Add(window ? "The coaching window is open: you can make changes now." : "Coaching changes happen between the end of the regular season and the next season.");
		if (((head == null) & window) && t.Manual)
		{
			staff.Urgent = true;
		}
		OfficeCard roster = card("Roster", "#roster", "Open the roster");
		List<Player> players = world.RosterOf(t.Id);
		List<string> hurt = new List<string>();
		foreach (Player p in players)
		{
			if (world.IsInjured(p, today))
			{
				hurt.Add(Rosters.PositionLabel(p.Position) + " " + p.ShortName);
			}
		}
		roster.Lines.Add("Team overall " + TeamOvr(t, players) + ", " + players.Count + " players");
		roster.Lines.Add((hurt.Count == 0) ? "Nobody injured." : ("Injured: " + string.Join(", ", hurt.ToArray())));
		if (SportRules.IsSchool(t.Level))
		{
			int leaving = players.FindAll((Player player) => player.ClassYear >= ((t.Level == Level.Junior) ? 3 : 4)).Count;
			roster.Lines.Add(leaving + ((leaving == 1) ? " player moves on" : " players move on") + " after this season.");
		}
		j.Name("overview").BeginArray();
		foreach (OfficeCard c in cards)
		{
			j.BeginObject().Prop("title", c.Title).Prop("tab", c.Tab)
				.Prop("button", c.Button)
				.Prop("urgent", c.Urgent);
			j.Name("lines").BeginArray();
			foreach (string line in c.Lines)
			{
				j.Value(line);
			}
			j.EndArray();
			j.EndObject();
		}
		j.EndArray();
	}

	public static string LeaguePanel(SportsWorld world, int now, bool timeKnown)
	{
		return LeaguePanel(world, now, timeKnown, new PanelView());
	}

	public static string LeaguePanel(SportsWorld world, int now, bool timeKnown, int selectedTeamId)
	{
		return LeaguePanel(world, now, timeKnown, new PanelView
		{
			TeamId = selectedTeamId
		});
	}

	public static string LeaguePanel(SportsWorld world, int now, bool timeKnown, PanelView view)
	{
		if (view == null)
		{
			view = new PanelView();
		}
		JsonBuilder j = new JsonBuilder();
		j.BeginObject();
		j.Prop("revision", world.Revision);
		j.Prop("dateLabel", timeKnown ? GameClock.MonthLabel(now) : "");
		j.Prop("notice", view.Notice ?? "");
		j.Prop("noticeOk", view.NoticeOk);
		j.Prop("page", view.LeagueKey ?? "");
		List<League> shown = new List<League>();
		foreach (League league in world.OrderedLeagues())
		{
			if (world.KeepsRunning(league) || league.PendingTeamIds.Count != 0 || league.Champions.Count != 0)
			{
				shown.Add(league);
			}
		}
		shown.Sort((League a, League b) => (a.Level == b.Level) ? a.Sport.CompareTo(b.Sport) : SportRules.Rank(b.Level).CompareTo(SportRules.Rank(a.Level)));
		j.Name("nav").BeginArray();
		foreach (League league2 in shown)
		{
			bool draftLive = league2.Level == Level.Pro && league2.Draft != null && league2.Draft.Started && !league2.Draft.Complete;
			Team waiting = world.WaitingOnUserPick(league2);
			string group;
			if (league2.Level == Level.Pro)
			{
				group = "Pro";
			}
			else if (league2.Level == Level.Minor)
			{
				group = "Minor leagues";
			}
			else if (league2.Level == Level.College)
			{
				group = "College";
			}
			else
			{
				group = ((league2.Level == Level.Junior) ? "Junior" : "High school");
			}
			JsonBuilder jsonBuilder = j.BeginObject().Prop("key", league2.Key).Prop("group", group)
				.Prop("title", LeagueTitle(league2));
			string value;
			if (waiting != null)
			{
				value = "Your pick";
			}
			else
			{
				value = (draftLive ? "Draft" : "");
			}
			jsonBuilder.Prop("badge", value).EndObject();
		}
		j.EndArray();
		j.Prop("userTeams", world.UserTeams().Count);
		j.Prop("hasIntl", world.Tournaments.Count > 0 || world.UserTeams().Count > 0);
		if (!string.IsNullOrEmpty(world.LoadProblem))
		{
			j.Prop("loadProblem", world.LoadProblem);
		}
		Team selected = world.GetTeam(view.TeamId);
		League page = null;
		foreach (League league3 in shown)
		{
			if (league3.Key == view.LeagueKey)
			{
				page = league3;
			}
		}
		Player player = world.AnyPlayer(view.PlayerId);
		if (view.PlayerId >= 0 && player == null)
		{
			j.Prop("playerMissing", value: true);
		}
		Match game = world.FindGame(view.GameId, out var gameLeague, out var gameSeason);
		if (view.GameId >= 0 && game == null)
		{
			j.Prop("gameMissing", value: true);
		}
		if (game != null)
		{
			j.Name("openGame").BeginObject().Prop("id", game.Id)
				.Prop("name", world.TeamAbbrev(game.AwayTeamId) + " at " + world.TeamAbbrev(game.HomeTeamId))
				.Prop("leagueKey", gameLeague.Key)
				.EndObject();
		}
		j.Name("office").BeginArray();
		foreach (Team t in world.UserTeams())
		{
			j.BeginObject().Prop("id", t.Id).Prop("name", world.TeamName(t.Id))
				.Prop("label", SportRules.LevelLabel(t.Sport, t.Level) + " " + SportRules.SportLabel(t.Sport))
				.Prop("level", (int)t.Level)
				.Prop("levelLabel", SportRules.LevelLabel(t.Sport, t.Level))
				.Prop("sportLabel", SportRules.SportLabel(t.Sport))
				.Prop("manual", t.Manual)
				.Prop("alerts", OfficeAlerts(world, t, now))
				.EndObject();
		}
		j.EndArray();
		if (selected != null)
		{
			j.Name("openTeam").BeginObject().Prop("id", selected.Id)
				.Prop("name", world.TeamName(selected.Id))
				.Prop("leagueKey", League.MakeKey(selected.Sport, selected.Level))
				.EndObject();
		}
		if (player != null)
		{
			j.Name("openPlayer").BeginObject().Prop("id", player.Id)
				.Prop("name", player.Name)
				.Prop("leagueKey", League.MakeKey(player.Sport, player.Level))
				.EndObject();
			WritePlayerView(j, world, player, now);
		}
		else if (game != null)
		{
			WriteGameView(j, world, gameLeague, gameSeason, game, now);
		}
		else if (selected != null)
		{
			WriteTeamView(j, world, selected, now, view);
		}
		else if (view.LeagueKey == "#feed")
		{
			WriteFeed(j, world);
		}
		else if (view.LeagueKey == "#intl")
		{
			WriteIntl(j, world, view.Tab ?? "");
		}
		else if (page != null)
		{
			WriteLeaguePage(j, world, page, now, view.Tab ?? "");
		}
		else
		{
			j.Name("myTeams").BeginArray();
			foreach (Team t2 in world.UserTeams())
			{
				WriteTeamSummary(j, world, t2, now);
			}
			j.EndArray();
		}
		j.EndObject();
		return j.ToString();
	}

	public static int TeamOvr(Team t, List<Player> roster)
	{
		if (roster == null || roster.Count == 0)
		{
			return 0;
		}
		return Rosters.Display(t.Level, Rosters.Strength(t.Sport, roster));
	}

	private static void WriteProSummary(JsonBuilder j, SportsWorld world, Team t, League league, int now)
	{
		bool pro = SportRules.ProLike(t.Sport, t.Level);
		j.Prop("isPro", pro);
		j.Prop("manual", t.Manual);
		Coach head = world.CoachOf(t.Id, CoachRole.Head);
		j.Prop("coach", (head == null) ? "" : (SportsWorld.RoleLabel(CoachRole.Head, t.Sport) + " " + head.Name + " (" + head.Overall + ")"));
		j.Prop("capLine", pro ? CapLine(world, t) : "");
		Team obj = ((league == null) ? null : world.WaitingOnUserPick(league));
		string draftNote = "";
		if (obj == t)
		{
			DraftPick pick = league.Draft.NextPick;
			draftNote = "You're on the clock in the " + SportsWorld.DraftYearLabel(league, league.Draft.Year) + " " + Economy.Get(league.Sport).DraftName + " with pick No. " + pick.Number + ". Pick before the month ends or your scouts will pick for you.";
		}
		j.Prop("draftNote", draftNote);
		string expiring = "";
		if (pro && t.Manual)
		{
			int n = 0;
			foreach (Player item in world.SignedPlayers(t.Id))
			{
				if (item.ContractYears == 1)
				{
					n++;
				}
			}
			if (n > 0)
			{
				expiring = n + ((n == 1) ? " contract ends" : " contracts end") + " after this season. Extend them on the Contracts page before free agency opens.";
			}
		}
		j.Prop("expiringNote", expiring);
		CalendarEvent next = ((league == null) ? null : world.NextEvent(league, now));
		j.Prop("nextEvent", (next == null) ? "" : (next.Title + ": " + next.Date));
	}

	public static string CapLine(SportsWorld world, Team t)
	{
		EconomyProfile economyProfile = Economy.Get(t.Sport);
		List<Player> signed = world.SignedPlayers(t.Id);
		int dead = Economy.DeadNow(t);
		int payroll = Economy.Payroll(signed) + dead;
		string deadText = ((dead > 0) ? (" incl. " + Economy.Money(dead) + " dead money") : "");
		return economyProfile.Type switch
		{
			CapType.Budget => "Payroll " + Economy.Money(payroll) + deadText + " of the owner's " + Economy.Money(Economy.Limit(t)) + " budget (luxury tax line " + Economy.Money(Economy.Cap(t.Sport)) + ")", 
			CapType.Mls => "Budget charge " + Economy.Money(Economy.CapHit(t.Sport, signed) + dead) + deadText + " of " + Economy.Money(Economy.Cap(t.Sport)) + ", " + Economy.DesignatedPlayers(t.Sport, signed) + " of 3 Designated Players, buyouts left " + Math.Max(0, 2 - t.Buyouts) + ", total payroll " + Economy.Money(payroll), 
			CapType.Soft => "Payroll " + Economy.Money(payroll) + deadText + " (soft cap " + Economy.Money(Economy.Cap(t.Sport)) + ((payroll > Economy.Cap(t.Sport)) ? ", over the cap" : (", " + Economy.Money(Economy.Cap(t.Sport) - payroll) + " of room")) + ")", 
			_ => "Payroll " + Economy.Money(payroll) + deadText + " of the " + Economy.Money(Economy.Cap(t.Sport)) + " hard cap" + ((payroll > Economy.Cap(t.Sport)) ? " (over: minimum contracts only)" : (" (" + Economy.Money(Economy.Cap(t.Sport) - payroll) + " of room)")), 
		};
	}

	private static void WriteFeed(JsonBuilder j, SportsWorld world)
	{
		Dictionary<string, string> titles = new Dictionary<string, string>();
		foreach (League l in world.Leagues)
		{
			titles[l.Key] = LeagueTitle(l);
		}
		j.Name("feed").BeginArray();
		for (int i = 0; i < world.Feed.Count && i < 150; i++)
		{
			NewsPost p = world.Feed[i];
			titles.TryGetValue(p.LeagueKey ?? "", out var league);
			j.BeginObject().Prop("id", p.Id).Prop("when", GameClock.MonthLabel(p.Time))
				.Prop("league", league ?? "")
				.Prop("leagueKey", p.LeagueKey ?? "")
				.Prop("kind", p.Kind.ToString())
				.Prop("account", p.Account)
				.Prop("handle", p.Handle)
				.Prop("text", p.Text)
				.Prop("isUser", p.IsUser)
				.Prop("likes", p.Likes)
				.Prop("reposts", p.Reposts)
				.EndObject();
		}
		j.EndArray();
	}

	private static string PositionName(Position p)
	{
		return p switch
		{
			Position.Center => "Center", 
			Position.LeftWing => "Left wing", 
			Position.RightWing => "Right wing", 
			Position.Defense => "Defenseman", 
			Position.Goalie => "Goalie", 
			Position.PointGuard => "Point guard", 
			Position.ShootingGuard => "Shooting guard", 
			Position.SmallForward => "Small forward", 
			Position.PowerForward => "Power forward", 
			Position.BigMan => "Center", 
			Position.Quarterback => "Quarterback", 
			Position.RunningBack => "Running back", 
			Position.WideReceiver => "Wide receiver", 
			Position.TightEnd => "Tight end", 
			Position.OffensiveLine => "Offensive lineman", 
			Position.DefensiveLine => "Defensive lineman", 
			Position.Linebacker => "Linebacker", 
			Position.Cornerback => "Cornerback", 
			Position.Safety => "Safety", 
			Position.Kicker => "Kicker", 
			Position.Punter => "Punter", 
			Position.StartingPitcher => "Starting pitcher", 
			Position.ReliefPitcher => "Relief pitcher", 
			Position.Catcher => "Catcher", 
			Position.FirstBase => "First baseman", 
			Position.SecondBase => "Second baseman", 
			Position.Shortstop => "Shortstop", 
			Position.ThirdBase => "Third baseman", 
			Position.LeftField => "Left fielder", 
			Position.CenterField => "Center fielder", 
			Position.RightField => "Right fielder", 
			Position.Goalkeeper => "Goalkeeper", 
			Position.Defender => "Defender", 
			Position.Midfielder => "Midfielder", 
			_ => "Forward", 
		};
	}

	private static string ClassName(int classYear)
	{
		return classYear switch
		{
			1 => "Freshman", 
			2 => "Sophomore", 
			3 => "Junior", 
			4 => "Senior", 
			_ => "Incoming", 
		};
	}

	private static string StatusText(SportsWorld world, Player p)
	{
		switch (p.Status)
		{
		case PlayerStatus.Prospect:
			return "In the minors (" + world.TeamName(p.TeamId) + " prospect)";
		case PlayerStatus.FreeAgent:
			return "Free agent";
		case PlayerStatus.Abroad:
			return "At " + (string.IsNullOrEmpty(p.From) ? "a club abroad" : p.From) + ", on the transfer market";
		case PlayerStatus.DraftPool:
			return "Draft prospect";
		case PlayerStatus.RecruitPool:
			if (p.Level != Level.Junior)
			{
				return "College recruit";
			}
			return "Junior recruit";
		case PlayerStatus.Retired:
		{
			Coach c = world.CoachFor(p);
			string now;
			if (c == null)
			{
				now = "";
			}
			else
			{
				now = ((c.TeamId >= 0) ? (". Now " + SportsWorld.RoleLabel(c.Role, c.Sport).ToLowerInvariant() + " of the " + world.TeamName(c.TeamId)) : ". Now a coach looking for work");
			}
			return "Retired after the " + p.RetiredYear + " season" + now;
		}
		default:
		{
			Team t = world.GetTeam(p.TeamId);
			Team parent = ((t == null) ? null : world.ParentOf(t));
			if (p.Level == Level.Minor && parent != null)
			{
				return "Active (farm team of the " + world.TeamName(parent.Id) + ")";
			}
			if (p.TeamId < 0)
			{
				return "";
			}
			return "Active";
		}
		}
	}

	private static Stats.StatGroup GroupFor(Player p)
	{
		Stats.StatGroup[] array = Stats.Groups(p.Sport);
		foreach (Stats.StatGroup g in array)
		{
			if (Array.IndexOf(g.Positions, p.Position) >= 0)
			{
				return g;
			}
		}
		return Stats.Groups(p.Sport)[0];
	}

	private static void WritePlayerView(JsonBuilder j, SportsWorld world, Player p, int now)
	{
		int num = GameClock.YearOfDay(GameClock.DayOf(now));
		Team team = world.GetTeam(p.TeamId);
		League league = world.GetLeague(p.Sport, p.Level);
		bool pro = p.Level == Level.Pro || p.Level == Level.Minor;
		int age = num - p.BirthYear;
		j.Name("playerView").BeginObject();
		j.Prop("id", p.Id);
		j.Prop("name", p.Name);
		j.Prop("number", p.Number);
		j.Prop("pos", Rosters.PositionLabel(p.Position));
		j.Prop("position", PositionName(p.Position));
		j.Prop("teamId", team?.Id ?? (-1));
		j.Prop("team", (team == null) ? "" : world.TeamName(team.Id));
		j.Prop("teamIsUser", team?.IsUser ?? false);
		j.Prop("leagueKey", League.MakeKey(p.Sport, p.Level));
		j.Prop("levelLabel", SportRules.LevelLabel(p.Sport, p.Level) + " " + SportRules.SportLabel(p.Sport));
		j.Prop("status", StatusText(world, p));
		j.Prop("ovr", Rosters.Ovr(p));
		j.Prop("pot", pro ? Rosters.Grade(p, age) : Rosters.Grade(p, 0));
		j.Prop("peak", Math.Max(p.PeakOvr, Rosters.Ovr(p)));
		j.Prop("titles", p.Titles);
		j.Prop("injury", world.InjuryText(p));
		j.Name("bio").BeginArray();
		Action<string, string> bio = (string value3, string value2) =>
		{
			if (!string.IsNullOrEmpty(value2))
			{
				j.BeginObject().Prop("label", value3).Prop("value", value2)
					.EndObject();
			}
		};
		bio("Position", PositionName(p.Position));
		if ((pro || p.Level == Level.Junior) && p.BirthYear > 1900)
		{
			bio("Age", age + " (born " + p.BirthYear + ")");
		}
		if (p.Level == Level.Junior && p.ClassYear > 0)
		{
			Action<string, string> action = bio;
			string arg;
			if (p.ClassYear == 1)
			{
				arg = "First";
			}
			else
			{
				arg = ((p.ClassYear == 2) ? "Second" : "Third");
			}
			action("Junior season", arg);
		}
		else if (!pro && p.Level != Level.Junior)
		{
			bio("Year", ClassName(p.ClassYear));
		}
		bio("Height", Bios.HeightLabel(Bios.ShownHeight(p)));
		int weight = Bios.ShownWeight(p);
		bio("Weight", (weight > 0) ? (weight + " lbs") : "");
		bio((p.Sport == Sport.Baseball && !Rosters.IsPitcher(p.Position)) ? "Bats and throws" : "Hand", p.Hand);
		bio("Hometown", world.HometownOf(p));
		bio("Came from", (string.IsNullOrEmpty(p.From) || p.From == "Prospect") ? "" : p.From);
		bio("Drafted", DraftText(world, p));
		if (pro)
		{
			bio("Contract", (p.ContractYears > 0) ? Economy.ContractText(p) : "");
		}
		bio("Cares most about", SportsWorld.PriorityLabel(p.Priority));
		RecruitTarget target = world.FindTarget(p.Id);
		if (target != null)
		{
			Action<string, string> action2 = bio;
			string text = target.Stars.ToString();
			string text2;
			if (target.CommittedTo == -2)
			{
				text2 = ", signed with a school outside the league";
			}
			else
			{
				text2 = ((target.CommittedTo >= 0) ? (", committed to " + world.TeamShortName(target.CommittedTo)) : ", uncommitted");
			}
			action2("Recruiting", text + "-star recruit" + text2);
			bio("What he wants", SportsWorld.WantsLabel(p.Priority));
		}
		j.EndArray();
		Stats.StatGroup group = GroupFor(p);
		j.Name("columns").BeginArray();
		string[] columns = group.Columns;
		foreach (string c in columns)
		{
			j.Value(c);
		}
		j.EndArray();
		j.Name("seasons").BeginArray();
		Dictionary<Level, int[]> totals = new Dictionary<Level, int[]>();
		Dictionary<Level, int> counts = new Dictionary<Level, int>();
		Action<Level, int[]> add = (Level key, int[] a) =>
		{
			if (!totals.TryGetValue(key, out var value2))
			{
				value2 = Stats.Empty(p.Sport);
				totals[key] = value2;
				counts[key] = 0;
			}
			Stats.AddInto(value2, Stats.Fit(a, p.Sport));
			counts[key]++;
		};
		foreach (SeasonLine l in p.History)
		{
			Team then = world.GetTeam(l.TeamId);
			j.BeginObject().Prop("season", l.Season).Prop("teamId", then?.Id ?? (-1))
				.Prop("team", (then != null) ? world.TeamAbbrev(then.Id) : l.Team)
				.Prop("teamName", (then != null) ? world.TeamName(then.Id) : l.Team)
				.Prop("level", SportRules.LevelLabel(p.Sport, l.Level))
				.Prop("ovr", l.Ovr)
				.Prop("current", value: false);
			j.Name("cells").BeginArray();
			columns = group.Cells(Stats.Fit(l.Stats, p.Sport));
			foreach (string c2 in columns)
			{
				j.Value(c2);
			}
			j.EndArray();
			j.EndObject();
			add(l.Level, l.Stats);
		}
		if (!Stats.IsEmpty(p.Season))
		{
			string label = ((league != null && league.Current != null) ? world.SeasonLabel(league, league.Current) : "This season");
			j.BeginObject().Prop("season", label + " (so far)").Prop("teamId", team?.Id ?? (-1))
				.Prop("team", (team != null) ? world.TeamAbbrev(team.Id) : "")
				.Prop("teamName", (team != null) ? world.TeamName(team.Id) : "")
				.Prop("level", SportRules.LevelLabel(p.Sport, p.Level))
				.Prop("ovr", Rosters.Ovr(p))
				.Prop("current", value: true);
			j.Name("cells").BeginArray();
			columns = group.Cells(Stats.Fit(p.Season, p.Sport));
			foreach (string c3 in columns)
			{
				j.Value(c3);
			}
			j.EndArray();
			j.EndObject();
			add(p.Level, p.Season);
		}
		j.EndArray();
		j.Name("careers").BeginArray();
		Level[] array = new Level[5]
		{
			Level.Pro,
			Level.Minor,
			Level.College,
			Level.Junior,
			Level.HighSchool
		};
		foreach (Level lv in array)
		{
			if (totals.TryGetValue(lv, out var t))
			{
				j.BeginObject().Prop("label", SportRules.LevelLabel(p.Sport, lv) + " career (" + counts[lv] + ((counts[lv] == 1) ? " season)" : " seasons)"));
				j.Name("cells").BeginArray();
				columns = group.Cells(t);
				foreach (string c4 in columns)
				{
					j.Value(c4);
				}
				j.EndArray();
				j.EndObject();
			}
		}
		j.EndArray();
		j.Name("playoffs").BeginArray();
		foreach (SeasonLine l2 in p.History)
		{
			if (!Stats.IsEmpty(l2.Playoff))
			{
				Team then2 = world.GetTeam(l2.TeamId);
				j.BeginObject().Prop("season", l2.Season).Prop("team", (then2 != null) ? world.TeamAbbrev(then2.Id) : l2.Team)
					.Prop("level", SportRules.LevelLabel(p.Sport, l2.Level));
				j.Name("cells").BeginArray();
				columns = group.Cells(Stats.Fit(l2.Playoff, p.Sport));
				foreach (string c5 in columns)
				{
					j.Value(c5);
				}
				j.EndArray();
				j.EndObject();
			}
		}
		j.EndArray();
		j.Prop("selections", SportsWorld.SelectionsLine(p));
		if (p.Sport == Sport.Soccer && SportRules.ProLike(p.Sport, p.Level) && (p.Status == PlayerStatus.Active || p.Status == PlayerStatus.Prospect || p.Status == PlayerStatus.Abroad))
		{
			j.Prop("transfer", "Worth " + Economy.Money(world.TransferValue(p)) + ", his club would want about " + Economy.Money(world.AskingFee(p)));
		}
		j.Name("honors").BeginArray();
		for (int i = p.Honors.Count - 1; i >= 0; i--)
		{
			if (SportsWorld.ParseHonor(p.Honors[i], out var season, out var award, out var trophy, out var lv2))
			{
				JsonBuilder jsonBuilder = j.BeginObject().Prop("season", season);
				string value;
				if (!(award == "Champion"))
				{
					value = award;
				}
				else
				{
					switch (lv2)
					{
					default:
						value = "State champion";
						break;
					case Level.Junior:
						value = "Junior Cup champion";
						break;
					case Level.College:
						value = "National champion";
						break;
					case Level.Pro:
					case Level.Minor:
						value = "League champion";
						break;
					}
				}
				jsonBuilder.Prop("award", value).Prop("trophy", trophy).Prop("level", SportRules.LevelLabel(p.Sport, lv2))
					.Prop("title", award == "Champion")
					.EndObject();
			}
		}
		j.EndArray();
		j.Name("injuries").BeginArray();
		foreach (string line in p.InjuryLog)
		{
			j.Value(line);
		}
		j.EndArray();
		string hall = "";
		League college = world.GetLeague(p.Sport, Level.College);
		if (college != null)
		{
			foreach (HallEntry e in college.HallOfFame)
			{
				if (e.PlayerId == p.Id)
				{
					hall = "College Hall of Fame, class of " + e.Inducted;
				}
			}
			foreach (HallEntry item in college.HallWaiting)
			{
				if (item.PlayerId == p.Id && hall.Length == 0)
				{
					hall = "Headed for the College Hall of Fame";
				}
			}
		}
		j.Prop("hall", hall);
		j.EndObject();
	}

	private static void WriteRecruiting(JsonBuilder j, SportsWorld world, Team t, League league, int now)
	{
		int month = GameClock.MonthIndex(now);
		int prestige = ((t.Prestige > 0) ? t.Prestige : 50);
		j.Prop("prestige", prestige);
		j.Prop("prestigeLabel", SportsWorld.PrestigeLabel(prestige));
		j.Prop("hours", t.RecruitHours);
		j.Prop("maxHours", world.RecruitHoursPerMonth(t));
		j.Prop("scholarships", world.Scholarships(t));
		j.Prop("offers", world.OffersMade(t));
		j.Name("needs").BeginArray();
		foreach (KeyValuePair<Position, int> kv in world.RecruitNeeds(t))
		{
			if (kv.Value > 0)
			{
				j.BeginObject().Prop("pos", Rosters.PositionLabel(kv.Key)).Prop("count", kv.Value)
					.EndObject();
			}
		}
		j.EndArray();
		List<KeyValuePair<RecruitTarget, Player>> rows = new List<KeyValuePair<RecruitTarget, Player>>();
		foreach (RecruitTarget r in world.RecruitBoard)
		{
			if (r.Sport == t.Sport)
			{
				Player p = world.GetPlayer(r.PlayerId);
				if (p != null)
				{
					rows.Add(new KeyValuePair<RecruitTarget, Player>(r, p));
				}
			}
		}
		rows.Sort((KeyValuePair<RecruitTarget, Player> a, KeyValuePair<RecruitTarget, Player> b) =>
		{
			bool flag = world.FindEffort(a.Value.Id, t.Id) != null || a.Key.CommittedTo == t.Id;
			bool flag2 = world.FindEffort(b.Value.Id, t.Id) != null || b.Key.CommittedTo == t.Id;
			if (flag != flag2)
			{
				if (!flag)
				{
					return 1;
				}
				return -1;
			}
			return (a.Key.Stars != b.Key.Stars) ? b.Key.Stars.CompareTo(a.Key.Stars) : b.Value.Overall.CompareTo(a.Value.Overall);
		});
		j.Prop("boardCount", rows.Count);
		j.Prop("committedCount", rows.FindAll((KeyValuePair<RecruitTarget, Player> x) => x.Key.CommittedTo == t.Id).Count);
		j.Name("recruits").BeginArray();
		int shown = 0;
		foreach (KeyValuePair<RecruitTarget, Player> kv2 in rows)
		{
			RecruitTarget r2 = kv2.Key;
			Player p2 = kv2.Value;
			RecruitEffort e = world.FindEffort(p2.Id, t.Id);
			if (e != null || r2.CommittedTo == t.Id || shown < 80)
			{
				shown++;
				bool thisMonth = e != null && e.Month == month;
				int interest = world.RecruitInterestIn(p2, t);
				string status;
				if (r2.CommittedTo == t.Id)
				{
					status = "Committed to you";
				}
				else if (r2.CommittedTo == -2)
				{
					status = "Signed outside the league";
				}
				else
				{
					status = ((r2.CommittedTo >= 0) ? ("Committed to " + world.TeamShortName(r2.CommittedTo)) : "Open");
				}
				bool open = r2.CommittedTo != t.Id && r2.CommittedTo != -2;
				int projected = ((p2.Level == Level.HighSchool) ? ((int)Math.Round(53.0 + (double)(p2.Overall - 55) * 0.7)) : p2.Overall);
				JsonBuilder jsonBuilder = j.BeginObject().Prop("id", p2.Id).Prop("name", p2.Name)
					.Prop("pos", Rosters.PositionLabel(p2.Position))
					.Prop("stars", r2.Stars)
					.Prop("ovr", Rosters.Display(Level.College, projected));
				string value;
				if (r2.National)
				{
					value = p2.From;
				}
				else
				{
					value = ((p2.From.Length > 0) ? p2.From : world.TeamShortName(p2.TeamId));
				}
				jsonBuilder.Prop("from", value).Prop("local", world.IsCityPlayer(p2) || (p2.Level == Level.HighSchool && world.GetTeam(p2.TeamId) != null && world.GetTeam(p2.TeamId).IsUser)).Prop("wants", SportsWorld.WantsLabel(p2.Priority))
					.Prop("interest", interest)
					.Prop("bar", SportsWorld.CommitBar(r2.Stars) + ((r2.CommittedTo >= 0 && r2.CommittedTo != t.Id) ? 10 : 0))
					.Prop("status", status)
					.Prop("mine", r2.CommittedTo == t.Id)
					.Prop("offered", e?.Offered ?? false)
					.Prop("targeted", e != null)
					.Prop("canCall", open && t.RecruitHours >= 1 && (!thisMonth || e.Calls < 2))
					.Prop("canWatch", open && t.RecruitHours >= 3 && (!thisMonth || !e.Watched))
					.Prop("canHome", open && t.RecruitHours >= 4 && (e == null || !e.HomeVisit))
					.Prop("canOfficial", open && t.RecruitHours >= 5 && e != null && e.Offered && !e.OfficialVisit)
					.Prop("canOffer", open && (e == null || !e.Offered))
					.EndObject();
			}
		}
		j.EndArray();
		j.Prop("recruitNote", "Recruits commit once their interest in your school passes the bar for their star level and you've offered a scholarship. Calls (1 hour, twice a month), watching him play (3 hours, once a month), a home visit (4 hours) and an official visit after an offer (5 hours) all raise interest, and time spent always helps, even with a recruit who doesn't fit your school. Effort you don't keep up fades a little each month. Prestige grows with winning and makes every recruit like you more. Other schools land recruits all year, top recruits first, and a recruit committed elsewhere can still flip if you outwork them. Signing day is the end of your season.");
	}

	private static void WriteTeamHistory(JsonBuilder j, SportsWorld world, Team t, League league)
	{
		string name = world.TeamName(t.Id);
		j.Name("titles").BeginArray();
		if (league != null)
		{
			foreach (ChampionRecord c in league.Champions)
			{
				if (c.TeamId == t.Id || (c.TeamId < 0 && c.TeamName == name))
				{
					j.BeginObject().Prop("season", c.SeasonLabel).Prop("runnerUp", c.RunnerUpName)
						.Prop("score", c.FinalScore)
						.EndObject();
				}
			}
		}
		j.EndArray();
		j.Prop("trophy", AwardBook.ChampionTrophy(t.Sport, t.Level));
		j.Name("rivals").BeginArray();
		if (league != null)
		{
			int shown = 0;
			foreach (Rivalry r in world.RivalriesOf(league, t.Id))
			{
				int other = ((r.TeamA == t.Id) ? r.TeamB : r.TeamA);
				if (world.GetTeam(other) == null)
				{
					continue;
				}
				int won = ((r.TeamA == t.Id) ? r.WinsA : r.WinsB);
				int lost = ((r.TeamA == t.Id) ? r.WinsB : r.WinsA);
				int sw = ((r.TeamA == t.Id) ? r.SeriesA : r.SeriesB);
				int sl = ((r.TeamA == t.Id) ? r.SeriesB : r.SeriesA);
				if (won + lost + r.Ties != 0 || sw + sl != 0)
				{
					string label = SportsWorld.RivalryLabel(r);
					j.BeginObject().Prop("teamId", other).Prop("name", world.TeamName(other))
						.Prop("label", label)
						.Prop("heat", r.Heat)
						.Prop("rival", r.Heat >= 50)
						.Prop("record", won + "-" + lost + ((r.Ties > 0) ? ("-" + r.Ties) : ""))
						.Prop("series", (sw + sl > 0) ? (sw + "-" + sl) : "")
						.Prop("last", r.LastMeeting ?? "")
						.EndObject();
					if (++shown >= 8)
					{
						break;
					}
				}
			}
		}
		j.EndArray();
		j.Name("retired").BeginArray();
		foreach (RetiredNumber r2 in t.RetiredNumbers)
		{
			j.BeginObject().Prop("number", r2.Number).Prop("name", r2.Name)
				.Prop("years", r2.Years)
				.Prop("season", r2.Season)
				.EndObject();
		}
		j.EndArray();
		j.Name("hall").BeginArray();
		foreach (HallEntry e in world.HallMembersOf(league, t.Id))
		{
			WriteHallEntry(j, e);
		}
		j.EndArray();
		j.Prop("historyNote", (t.Level == Level.HighSchool || t.Level == Level.Junior) ? "Rivalries grow from close games and playoff meetings, and cool off when teams stop meeting." : "Rivalries grow from close games and playoff meetings, and cool off when teams stop meeting. Players with a great career here go into the Hall of Fame and get their number retired.");
	}

	private static void WriteHallEntry(JsonBuilder j, HallEntry e)
	{
		j.BeginObject().Prop("name", e.Name).Prop("pos", e.Position)
			.Prop("teams", e.Teams)
			.Prop("years", e.Years)
			.Prop("summary", e.Summary)
			.Prop("inducted", e.Inducted)
			.Prop("eligible", e.EligibleYear)
			.Prop("number", e.Number)
			.EndObject();
	}

	private static void WriteHallPage(JsonBuilder j, SportsWorld world, League league)
	{
		j.Prop("hallNote", "When a " + ((league.Level == Level.Pro) ? "player retires" : "player leaves college") + ", his career is scored: seasons, awards (player of the year counts the most), titles and how good he was at his best. It takes at least one award to get in. The great ones are voted in after " + ((league.Level == Level.Pro) ? "3 seasons" : "2 seasons") + ", up to " + ((league.Level == Level.Pro) ? 4 : 3) + " a year, and their team retires their number.");
		j.Name("members").BeginArray();
		foreach (HallEntry e in league.HallOfFame)
		{
			WriteHallEntry(j, e);
		}
		j.EndArray();
		j.Name("ballot").BeginArray();
		List<HallEntry> list = new List<HallEntry>(league.HallWaiting);
		list.Sort((HallEntry hallEntry, HallEntry hallEntry2) => (hallEntry.EligibleYear == hallEntry2.EligibleYear) ? hallEntry2.Score.CompareTo(hallEntry.Score) : hallEntry.EligibleYear.CompareTo(hallEntry2.EligibleYear));
		foreach (HallEntry e2 in list)
		{
			WriteHallEntry(j, e2);
		}
		j.EndArray();
		j.Name("rivalries").BeginArray();
		List<Rivalry> list2 = new List<Rivalry>(league.Rivalries);
		list2.Sort((Rivalry rivalry2, Rivalry rivalry) => rivalry.Heat.CompareTo(rivalry2.Heat));
		int shown = 0;
		foreach (Rivalry r in list2)
		{
			if (r.Heat >= 30 && world.GetTeam(r.TeamA) != null && world.GetTeam(r.TeamB) != null)
			{
				Team a = world.GetTeam(r.TeamA);
				Team b = world.GetTeam(r.TeamB);
				j.BeginObject().Prop("teamA", r.TeamA).Prop("nameA", world.TeamName(r.TeamA))
					.Prop("teamB", r.TeamB)
					.Prop("nameB", world.TeamName(r.TeamB))
					.Prop("label", SportsWorld.RivalryLabel(r))
					.Prop("heat", r.Heat)
					.Prop("record", r.WinsA + "-" + r.WinsB + ((r.Ties > 0) ? ("-" + r.Ties) : ""))
					.Prop("series", (r.SeriesA + r.SeriesB > 0) ? (r.SeriesA + "-" + r.SeriesB) : "")
					.Prop("isUser", a.IsUser || b.IsUser)
					.EndObject();
				if (++shown >= 10)
				{
					break;
				}
			}
		}
		j.EndArray();
	}

	private static List<KeyValuePair<string, string>> TeamTabs(Team t)
	{
		bool flag = SportRules.ProLike(t.Sport, t.Level);
		List<KeyValuePair<string, string>> tabs = new List<KeyValuePair<string, string>>
		{
			new KeyValuePair<string, string>("", "Roster")
		};
		if (flag)
		{
			tabs.Add(new KeyValuePair<string, string>("contracts", "Contracts"));
		}
		tabs.Add(new KeyValuePair<string, string>("staff", "Coaches"));
		tabs.Add(new KeyValuePair<string, string>("schedule", "Schedule"));
		tabs.Add(new KeyValuePair<string, string>("history", "History"));
		return tabs;
	}

	private static void WriteTeamView(JsonBuilder j, SportsWorld world, Team t, int now, PanelView view)
	{
		League league = world.LeagueOf(t);
		Organization o = world.GetOrg(t.OrgId);
		List<Player> roster = world.RosterOf(t.Id);
		bool pro = SportRules.ProLike(t.Sport, t.Level);
		string tab = view.TeamTab ?? "";
		bool office = view.Office && t.IsUser;
		List<KeyValuePair<string, string>> tabs = (office ? OfficeTabs(t) : TeamTabs(t));
		if (!tabs.Exists((KeyValuePair<string, string> keyValuePair) => keyValuePair.Key == tab))
		{
			tab = "";
		}
		j.Name("teamView").BeginObject();
		j.Prop("id", t.Id);
		j.Prop("name", (o == null) ? "TBD" : o.FullName);
		j.Prop("title", SportRules.LevelLabel(t.Sport, t.Level) + " " + SportRules.SportLabel(t.Sport));
		j.Prop("isUser", t.IsUser);
		j.Prop("isPro", pro);
		j.Prop("leagueKey", League.MakeKey(t.Sport, t.Level));
		j.Prop("overall", TeamOvr(t, roster));
		j.Prop("rating", t.Rating);
		JsonBuilder jsonBuilder = j;
		string value;
		if (pro)
		{
			value = (world.IsContending(t) ? "Contending" : "Rebuilding");
		}
		else
		{
			value = "";
		}
		jsonBuilder.Prop("mode", value);
		j.Prop("hot", (t.Cinderella > 0) ? "On a Cinderella run" : "");
		JsonBuilder jsonBuilder2 = j;
		string value2;
		if (t.Retired && t.FoldedYear > 0)
		{
			value2 = "Folded in " + t.FoldedYear + ". Its history, awards and records stay in the books.";
		}
		else
		{
			value2 = (t.Retired ? "No longer in the league." : "");
		}
		jsonBuilder2.Prop("folded", value2);
		Season s = ((league == null) ? null : (league.Current ?? league.Previous));
		bool lastSeason = league != null && league.Previous != null && (league.Current == null || !league.Current.AnyPlayed) && league.Previous.Games.Exists((Match g) => g.Involves(t.Id) && g.Played);
		if (lastSeason)
		{
			s = league.Previous;
		}
		string record = "";
		string standing = "";
		if (s != null && league.TeamIds.Contains(t.Id))
		{
			List<StandingRow> table = Standings.Compute(league, s);
			foreach (StandingRow r in table)
			{
				if (r.TeamId == t.Id)
				{
					record = Standings.RecordText(t.Sport, r);
					standing = ((r.Played == 0) ? "" : ((lastSeason ? "Finished " : "") + Ordinal(r.Rank) + " of " + table.Count + (lastSeason ? " last season" : "")));
				}
			}
		}
		j.Prop("record", record);
		if (league != null && t.VenueId >= 0)
		{
			world.HomeCrowds(league, t, out var hg, out var avg, out var sold, out var cap);
			if (hg > 0)
			{
				j.Prop("crowds", "Home crowds: " + avg.ToString("N0", CultureInfo.InvariantCulture) + " a game (" + Math.Min(100, (int)Math.Round(100.0 * (double)avg / (double)cap)) + "% of " + cap.ToString("N0", CultureInfo.InvariantCulture) + " seats), " + sold + ((sold == 1) ? " sellout" : " sellouts") + " in " + hg + " home games");
			}
		}
		j.Prop("standing", standing);
		Coach head = world.CoachOf(t.Id, CoachRole.Head);
		j.Prop("coach", (head == null) ? "" : head.Name);
		Team parent = world.ParentOf(t);
		Team farm = world.AffiliateOf(t);
		if (parent != null)
		{
			j.Prop("parentId", parent.Id).Prop("parentName", world.TeamName(parent.Id));
		}
		if (farm != null)
		{
			j.Prop("farmId", farm.Id).Prop("farmName", world.TeamName(farm.Id));
		}
		j.Prop("manual", t.Manual);
		j.Prop("canManage", t.IsUser);
		JsonBuilder jsonBuilder3 = j;
		string value3;
		if (t.IsUser)
		{
			if (t.Level == Level.Minor && !pro)
			{
				value3 = (t.Manual ? "You choose this team's coaches. Players move between this team and its pro club (call-ups and send-downs), and new players sign every offseason." : "The front office hires and fires this team's coaches. Players move between this team and its pro club (call-ups and send-downs), and new players sign every offseason.");
			}
			else if (pro)
			{
				value3 = (t.Manual ? ("You run this team: contracts, free agents, " + ((t.Sport == Sport.Soccer) ? "transfers" : "trades") + ", the draft and coaches. Holes in the roster are still filled automatically so games can be played.") : ("The AI general manager runs this team: re-signings, free agents, " + ((t.Sport == Sport.Soccer) ? "transfers" : "trades") + ", draft picks and coaches happen on their own."));
			}
			else
			{
				value3 = (t.Manual ? "You choose this team's coaches (between the end of the regular season and the next season). Players come and go through recruiting and graduation." : "The athletic director hires and fires this team's coaches. Players come and go through recruiting and graduation.");
			}
		}
		else
		{
			value3 = "";
		}
		jsonBuilder3.Prop("manageText", value3);
		j.Prop("office", office);
		j.Prop("alerts", t.IsUser ? OfficeAlerts(world, t, now) : 0);
		j.Prop("tab", tab);
		j.Name("tabs").BeginArray();
		foreach (KeyValuePair<string, string> x in tabs)
		{
			j.BeginObject().Prop("id", x.Key).Prop("label", x.Value)
				.EndObject();
		}
		j.EndArray();
		if (office)
		{
			switch (tab)
			{
			case "contracts":
				WriteContracts(j, world, t, league, now, office: true);
				break;
			case "fa":
				if (league != null)
				{
					WriteFreeAgents(j, world, league, now);
				}
				break;
			case "draft":
				if (league != null)
				{
					WriteDraft(j, world, league, now);
				}
				break;
			case "staff":
				WriteStaff(j, world, t, league, now, office: true);
				break;
			case "trade":
				WriteTrade(j, world, t, league, now, view);
				break;
			case "transfers":
				if (league != null)
				{
					WriteTransfers(j, world, t, league, now);
				}
				break;
			case "recruiting":
				WriteRecruiting(j, world, t, league, now);
				break;
			case "farm":
				WriteFarm(j, world, t, league, now);
				break;
			default:
				WriteOverview(j, world, t, league, now);
				break;
			}
		}
		else
		{
			switch (tab)
			{
			case "contracts":
				WriteContracts(j, world, t, league, now, office: false);
				break;
			case "staff":
				WriteStaff(j, world, t, league, now, office: false);
				break;
			case "schedule":
				WriteTeamSchedule(j, world, t, league);
				break;
			case "history":
				WriteTeamHistory(j, world, t, league);
				break;
			default:
				WriteRoster(j, world, t, league, now);
				break;
			}
		}
		j.EndObject();
	}

	private static void WriteRoster(JsonBuilder j, SportsWorld world, Team t, League league, int now)
	{
		List<Player> roster = world.RosterOf(t.Id);
		bool last = league == null || world.ShowingLastSeason(league);
		Season statSeason;
		if (league == null)
		{
			statSeason = null;
		}
		else
		{
			statSeason = (last ? league.Previous : league.Current);
		}
		int year = GameClock.YearOfDay(GameClock.DayOf(now));
		JsonBuilder jsonBuilder = j;
		string value;
		if (last)
		{
			value = ((statSeason != null) ? ("Last season (" + world.SeasonLabel(league, statSeason) + ")") : "No games played yet");
		}
		else
		{
			value = ((league != null && league.Current != null) ? (world.SeasonLabel(league, league.Current) + " season") : "This season");
		}
		jsonBuilder.Prop("statsTitle", value);
		j.Prop("ageLabel", (t.Level == Level.Pro || t.Level == Level.Junior || t.Level == Level.Minor) ? "Age" : "Year");
		j.Name("groups").BeginArray();
		Stats.StatGroup[] array = Stats.Groups(t.Sport);
		foreach (Stats.StatGroup group in array)
		{
			List<Player> players = roster.FindAll((Player player) => Array.IndexOf(group.Positions, player.Position) >= 0);
			if (players.Count == 0)
			{
				continue;
			}
			players.Sort((Player a, Player b) => (b.Overall == a.Overall) ? a.Id.CompareTo(b.Id) : b.Overall.CompareTo(a.Overall));
			j.BeginObject();
			j.Prop("name", group.Name);
			j.Name("columns").BeginArray();
			string[] columns = group.Columns;
			foreach (string c in columns)
			{
				j.Value(c);
			}
			j.EndArray();
			j.Name("players").BeginArray();
			foreach (Player p in players)
			{
				int[] line = (last ? p.LastSeason : p.Season);
				int[] career = (int[])Stats.Fit(p.Career, t.Sport).Clone();
				if (!last)
				{
					Stats.AddInto(career, Stats.Fit(p.Season, t.Sport));
				}
				j.BeginObject();
				WritePlayerBasics(j, world, t, p, year);
				j.Prop("seasons", p.Seasons + ((!last && !Stats.IsEmpty(p.Season)) ? 1 : 0));
				j.Name("cells").BeginArray();
				columns = group.Cells(Stats.Fit(line, t.Sport));
				foreach (string c2 in columns)
				{
					j.Value(c2);
				}
				j.EndArray();
				j.Name("career").BeginArray();
				columns = group.Cells(career);
				foreach (string c3 in columns)
				{
					j.Value(c3);
				}
				j.EndArray();
				j.EndObject();
			}
			j.EndArray();
			j.EndObject();
		}
		j.EndArray();
		if (!SportRules.ProLike(t.Sport, t.Level))
		{
			return;
		}
		j.Name("prospects").BeginArray();
		List<Player> list = new List<Player>(world.ProspectsOf(t.Id));
		list.Sort((Player a, Player b) => (b.Potential == a.Potential) ? a.Id.CompareTo(b.Id) : b.Potential.CompareTo(a.Potential));
		foreach (Player p2 in list)
		{
			j.BeginObject();
			WritePlayerBasics(j, world, t, p2, year);
			j.EndObject();
		}
		j.EndArray();
	}

	private static void WritePlayerBasics(JsonBuilder j, SportsWorld world, Team t, Player p, int year)
	{
		int age = year - p.BirthYear;
		bool pro = t.Level == Level.Pro || t.Level == Level.Minor;
		j.Prop("id", p.Id);
		j.Prop("number", p.Number);
		j.Prop("name", p.Name);
		j.Prop("pos", Rosters.PositionLabel(p.Position));
		j.Prop("age", (pro || t.Level == Level.Junior) ? age.ToString() : Rosters.ClassLabel(p.ClassYear));
		j.Prop("ovr", Rosters.Ovr(p));
		j.Prop("pot", pro ? Rosters.Grade(p, age) : Rosters.Grade(p, 0));
		j.Prop("change", Rosters.Ovr(p) - Rosters.Display(p.Sport, p.Level, p.PrevOverall));
		j.Prop("priority", SportsWorld.PriorityLabel(p.Priority));
		j.Prop("contract", SportRules.ProLike(t.Sport, t.Level) ? Economy.ContractText(p) : "");
		j.Prop("from", (string.IsNullOrEmpty(p.From) || p.From == "Prospect") ? "" : p.From);
		j.Prop("draft", DraftText(world, p));
		j.Prop("reserve", p.Status == PlayerStatus.Prospect);
		j.Prop("injury", world.InjuryText(p));
	}

	private static string DraftText(SportsWorld world, Player p)
	{
		if (p.DraftYear <= 0)
		{
			return "";
		}
		League league = world.GetLeague(p.Sport, Level.Pro);
		int year = ((league == null) ? (p.DraftYear + 1) : SportsWorld.DraftYearLabel(league, p.DraftYear));
		return year + " draft, round " + p.DraftRound + ", pick " + p.DraftPick + " by the " + world.TeamName(p.DraftTeamId);
	}

	private static void WriteTalk(JsonBuilder j, SportsWorld world, ContractTalk k, Player p, Team t)
	{
		EconomyProfile e = Economy.Get(t.Sport);
		j.Name("talk").BeginObject().Prop("ask", k.Ask)
			.Prop("askLabel", Economy.Money(k.Ask))
			.Prop("askYears", k.AskYears)
			.Prop("interest", k.Interest)
			.Prop("patience", k.Patience)
			.Prop("status", (k.Interest <= 0) ? 3 : k.Status)
			.Prop("message", k.Message ?? "")
			.Prop("mood", (k.Rounds > 0) ? SportsWorld.Mood(k) : "")
			.Prop("offer", k.Offer)
			.Prop("offerYears", k.OfferYears)
			.Prop("rounds", k.Rounds)
			.Prop("min", e.MinSalary)
			.Prop("max", Economy.MaxSalary(t.Sport))
			.Prop("maxYears", k.Extension ? e.MaxYearsResign : e.MaxYearsFree)
			.Prop("extension", k.Extension)
			.Prop("teamId", t.Id)
			.Prop("playerId", p.Id)
			.EndObject();
	}

	private static void WriteContracts(JsonBuilder j, SportsWorld world, Team t, League league, int now, bool office)
	{
		int year = GameClock.YearOfDay(GameClock.DayOf(now));
		bool mine = office && t.IsUser && t.Manual;
		j.Prop("capLine", CapLine(world, t));
		EconomyProfile e = Economy.Get(t.Sport);
		string fa = "";
		if (league != null)
		{
			foreach (CalendarEvent ev in world.Calendar(league, now))
			{
				if (ev.Title == "Free agency opens" && ev.State != 0)
				{
					fa = ev.Date;
					break;
				}
			}
		}
		JsonBuilder jsonBuilder = j;
		string value;
		if (office)
		{
			if (t.IsUser)
			{
				value = (t.Manual ? ("Players in the last year of their deal can be extended until free agency opens" + ((fa.Length > 0) ? (" (" + fa + ")") : "") + ". What they ask depends on what they care about: loyal players give a discount, winning-first players won't stay with a team near the bottom.") : "Choose \"Run this team myself\" to handle contracts. Until then the AI general manager re-signs and releases players.");
			}
			else
			{
				value = "";
			}
		}
		else
		{
			value = (t.IsUser ? "Talk contracts, release players and see dead money in the Front office." : "");
		}
		jsonBuilder.Prop("resignNote", value);
		List<Player> list = world.SignedPlayers(t.Id);
		list.Sort((Player player, Player b) => (b.Salary == player.Salary) ? player.Id.CompareTo(b.Id) : b.Salary.CompareTo(player.Salary));
		j.Name("players").BeginArray();
		foreach (Player p in list)
		{
			j.BeginObject();
			WritePlayerBasics(j, world, t, p, year);
			j.Prop("salary", Economy.Money(p.Salary));
			j.Prop("years", p.ContractYears);
			j.Prop("expiring", p.ContractYears <= 1);
			string resign = "";
			bool canResign = false;
			if (mine && p.ContractYears <= 1)
			{
				ContractTalk k = world.TalkOrPreview(p, t, extension: true, now);
				if (k.Interest <= 0)
				{
					resign = "Won't extend: wants a contender";
				}
				else if (k.Status == 2)
				{
					resign = "Walked out on extension talks. He'll test free agency.";
				}
				else
				{
					resign = "Asks " + Economy.Money(k.Ask) + " a season for " + k.AskYears + ((k.AskYears == 1) ? " season" : " seasons");
					canResign = true;
				}
				WriteTalk(j, world, k, p, t);
			}
			j.Prop("resign", resign);
			j.Prop("canResign", canResign);
			j.Prop("canRelease", mine);
			if (mine)
			{
				ReleaseQuote q = world.QuoteRelease(p, t, now, stretch: false);
				j.Prop("releaseLabel", q.Label);
				JsonBuilder jsonBuilder2 = j;
				string value2;
				if (q.Total > 0)
				{
					value2 = q.Short;
				}
				else
				{
					value2 = (q.UsesBuyout ? "Free: uses a buyout." : "Free: no dead money.");
				}
				jsonBuilder2.Prop("releaseCost", value2);
				j.Prop("releaseRule", q.Rule + ((q.Saved > 0) ? (" Saves " + Economy.Money(q.Saved) + " in salary.") : ""));
				j.Prop("releaseFree", q.Total <= 0);
				if (q.CanStretch)
				{
					ReleaseQuote st = world.QuoteRelease(p, t, now, stretch: true);
					j.Prop("stretchCost", st.Short);
				}
			}
			j.EndObject();
		}
		j.EndArray();
		j.Name("dead").BeginArray();
		foreach (DeadCharge c in t.Dead)
		{
			int total = 0;
			foreach (int a in c.Amounts)
			{
				total += a;
			}
			j.BeginObject().Prop("name", c.Name).Prop("reason", c.Reason)
				.Prop("now", Economy.Money((c.Amounts.Count > 0) ? c.Amounts[0] : 0))
				.Prop("total", Economy.Money(total))
				.Prop("seasons", c.Amounts.Count)
				.Prop("playerId", c.PlayerId)
				.EndObject();
		}
		j.EndArray();
		j.Prop("deadNow", (Economy.DeadNow(t) > 0) ? Economy.Money(Economy.DeadNow(t)) : "");
		j.Prop("deadLater", (Economy.DeadLater(t) > 0) ? Economy.Money(Economy.DeadLater(t)) : "");
		j.Prop("releaseRules", SportsWorld.ReleaseRules(t.Sport));
		JsonBuilder jsonBuilder3 = j;
		string value3;
		if (e.Type != CapType.Hard)
		{
			if (e.Type == CapType.Soft)
			{
				value3 = "Soft cap: teams can go over the cap to keep their own players.";
			}
			else
			{
				value3 = ((e.Type == CapType.Budget) ? "No cap: each owner sets a budget. Going over the luxury tax line is allowed." : "Salary budget with up to 3 Designated Players whose salaries count only partly.");
			}
		}
		else
		{
			value3 = "Hard cap: payroll can't go over the cap except for minimum contracts.";
		}
		jsonBuilder3.Prop("payrollNote", value3);
	}

	private static void WriteStaff(JsonBuilder j, SportsWorld world, Team t, League league, int now, bool office)
	{
		int year = world.CurrentYear();
		bool window = league == null || world.CoachingWindowOpen(league, now);
		bool canChange = (office && t.IsUser && t.Manual) & window;
		j.Prop("windowOpen", window);
		j.Prop("canChange", canChange);
		JsonBuilder jsonBuilder = j;
		string value;
		if (t.IsUser)
		{
			if (office)
			{
				if (t.Manual)
				{
					value = (window ? "The coaching window is open: let a coach go and hire a new one from the list below." : ("Coaching changes happen between the end of the regular season and " + (SportRules.ProLike(t.Sport, t.Level) ? "the start of free agency." : "the next season's first game.")));
				}
				else
				{
					value = "Choose \"" + (SportRules.ProLike(t.Sport, t.Level) ? "Run this team myself" : "Choose the coaches myself") + "\" to hire and fire coaches.";
				}
			}
			else
			{
				value = "Hire and fire coaches in the Front office.";
			}
		}
		else
		{
			value = "";
		}
		jsonBuilder.Prop("staffNote", value);
		j.Name("staff").BeginArray();
		foreach (Coach c in world.StaffOf(t.Id))
		{
			WriteCoachCard(j, c, year, world, where: false);
		}
		j.EndArray();
		j.Name("effects").BeginArray();
		j.Value("Game plans (tactics): " + Signed(world.TacticsBonus(t), 1) + " rating points in every game.");
		j.Value("Motivation: hot and cold streaks count " + Math.Round(world.MotivationFactor(t) * 100.0) + "% as much as usual.");
		if (SportRules.ProLike(t.Sport, t.Level))
		{
			j.Value("Development: " + Signed(world.DevelopmentShift(t), 1) + " rating points a year for the roster.");
		}
		else
		{
			j.Value("Development: players close " + Signed(world.SchoolDevelopmentBoost(t) * 100.0, 1) + "% more of the gap to their potential each year.");
		}
		j.Value(SportRules.ProLike(t.Sport, t.Level) ? ("Recruiting: " + Signed(world.RecruitingEdge(t), 1) + " points of appeal to free agents.") : ("Recruiting: " + Signed(world.RecruitingEdge(t), 1) + " points in the race for recruits."));
		j.EndArray();
		j.Name("jobs").BeginArray();
		CoachRole[] array = ((t.Sport != Sport.Football) ? new CoachRole[1] : new CoachRole[3]
		{
			CoachRole.Head,
			CoachRole.Offense,
			CoachRole.Defense
		});
		foreach (CoachRole role in array)
		{
			if (world.CoachOf(t.Id, role) != null || !canChange)
			{
				continue;
			}
			j.BeginObject();
			j.Prop("roleId", (int)role);
			j.Prop("role", SportsWorld.RoleLabel(role, t.Sport));
			j.Name("candidates").BeginArray();
			foreach (Coach c2 in world.CoachCandidates(t, role))
			{
				WriteCoachCard(j, c2, year, world, where: true);
			}
			j.EndArray();
			j.EndObject();
		}
		j.EndArray();
	}

	private static string Signed(double v, int digits)
	{
		double r = Math.Round(v, digits);
		string text = r.ToString((digits == 0) ? "0" : "0.0", CultureInfo.InvariantCulture);
		if (!(r > 0.0))
		{
			return text;
		}
		return "+" + text;
	}

	private static void WriteCoachCard(JsonBuilder j, Coach c, int year, SportsWorld world, bool where)
	{
		j.BeginObject();
		j.Prop("id", c.Id);
		j.Prop("roleId", (int)c.Role);
		j.Prop("role", SportsWorld.RoleLabel(c.Role, c.Sport));
		j.Prop("name", c.Name);
		j.Prop("age", year - c.BirthYear);
		j.Prop("ovr", c.Overall);
		j.Prop("change", c.Overall - c.PrevOverall);
		j.Prop("grade", SportsWorld.CoachGrade(c));
		j.Prop("dev", c.Development);
		j.Prop("tac", c.Tactics);
		j.Prop("mot", c.Motivation);
		j.Prop("rec", c.Recruiting);
		j.Prop("seasons", c.Seasons);
		j.Prop("record", CoachRecord(c));
		j.Prop("playoffs", (c.PlayoffWins + c.PlayoffLosses == 0) ? "" : (c.PlayoffWins + "-" + c.PlayoffLosses));
		j.Prop("titles", c.Titles);
		j.Prop("history", c.History ?? "");
		j.Prop("since", (c.TeamId >= 0) ? c.HiredYear : 0);
		string now = "";
		if (where)
		{
			now = ((c.TeamId >= 0) ? (SportsWorld.RoleLabel(c.Role, c.Sport) + ", " + world.TeamName(c.TeamId) + " (" + SportRules.LevelLabel(c.Level) + ")") : "Looking for work");
		}
		j.Prop("where", now);
		WriteCoachOrigins(j, world, c);
		j.EndObject();
	}

	private static void WriteCoachOrigins(JsonBuilder j, SportsWorld world, Coach c)
	{
		Player stand = new Player
		{
			Hometown = (c.Hometown ?? "")
		};
		j.Prop("hometown", Bios.HometownLabel(stand, world.CityName));
		j.Prop("played", string.IsNullOrEmpty(c.Played) ? "" : ("Ex-pro: " + c.Played));
		j.Prop("playerId", (c.FormerPlayerId >= 0 && world.Retired.ContainsKey(c.FormerPlayerId)) ? c.FormerPlayerId : (-1));
	}

	private static void WriteTrade(JsonBuilder j, SportsWorld world, Team t, League league, int now, PanelView view)
	{
		int year = world.CurrentYear();
		bool open = league != null && world.TradeWindowOpen(league, now);
		j.Prop("tradeOpen", open);
		JsonBuilder jsonBuilder = j;
		string value;
		if (league == null)
		{
			value = "";
		}
		else if (open)
		{
			value = ((league.Current != null && league.Current.AnyPlayed) ? ("Trade window open until the deadline, " + world.TradeDeadlineLabel(league) + ".") : "The trade window is open.");
		}
		else
		{
			value = "The trade window is closed until the playoffs are over.";
		}
		jsonBuilder.Prop("tradeNote", value);
		j.Prop("capLine", CapLine(world, t));
		if (league == null)
		{
			return;
		}
		string mode = ((view.TradeMode == "finder" || view.TradeMode == "block") ? view.TradeMode : "");
		j.Prop("tradeMode", mode);
		j.Prop("offerCount", world.OffersFor(t.Id).Count);
		if (mode == "finder")
		{
			WriteFinder(j, world, t, league, now, view);
			return;
		}
		if (mode == "block")
		{
			WriteBlock(j, world, t, league, now);
			return;
		}
		j.Name("partners").BeginArray();
		Season s = league.Current ?? league.Previous;
		Dictionary<int, string> records = new Dictionary<int, string>();
		if (s != null)
		{
			foreach (StandingRow r in Standings.Compute(league, s))
			{
				records[r.TeamId] = Standings.RecordText(league.Sport, r);
			}
		}
		foreach (int id in league.TeamIds)
		{
			Team other = world.GetTeam(id);
			if (other != null && other != t && !other.Retired)
			{
				records.TryGetValue(id, out var rec);
				j.BeginObject().Prop("id", id).Prop("name", world.TeamName(id))
					.Prop("mode", world.IsContending(other) ? "Contending" : "Rebuilding")
					.Prop("record", rec ?? "")
					.Prop("mine", other.IsUser && other.Manual)
					.EndObject();
			}
		}
		j.EndArray();
		Team partner = world.GetTeam(view.PartnerId);
		if (partner == null || partner == t || world.LeagueOf(partner) != league)
		{
			return;
		}
		j.Name("trade").BeginObject();
		j.Prop("partnerId", partner.Id);
		j.Prop("partnerName", world.TeamName(partner.Id));
		bool contending = world.IsContending(partner);
		j.Prop("partnerMode", contending ? "They're contending: they want players who help right now, and care less about picks." : "They're rebuilding: they want young players with upside and draft picks.");
		j.Prop("partnerCap", CapLine(world, partner));
		j.Name("mine").BeginArray();
		List<Player> list = world.SignedPlayers(t.Id);
		list.Sort((Player player, Player b) => (b.Overall == player.Overall) ? player.Id.CompareTo(b.Id) : b.Overall.CompareTo(player.Overall));
		foreach (Player p in list)
		{
			WriteTradePlayer(j, world, t, partner, p, year);
		}
		j.EndArray();
		j.Name("theirs").BeginArray();
		List<Player> list2 = world.SignedPlayers(partner.Id);
		list2.Sort((Player player, Player b) => (b.Overall == player.Overall) ? player.Id.CompareTo(b.Id) : b.Overall.CompareTo(player.Overall));
		foreach (Player p2 in list2)
		{
			WriteTradePlayer(j, world, partner, partner, p2, year);
		}
		j.EndArray();
		j.Name("myPicks").BeginArray();
		foreach (PickAsset a in world.TradeablePicks(league, t.Id))
		{
			WriteTradePick(j, world, league, partner, a);
		}
		j.EndArray();
		j.Name("theirPicks").BeginArray();
		foreach (PickAsset a2 in world.TradeablePicks(league, partner.Id))
		{
			WriteTradePick(j, world, league, partner, a2);
		}
		j.EndArray();
		bool num = view.Give.Count + view.GivePicks.Count > 0 || view.Get.Count + view.GetPicks.Count > 0;
		j.Name("preview").BeginObject();
		if (num)
		{
			TradeCheck c = world.CheckTrade(t.Id, partner.Id, view.Give, view.Get, view.GivePicks, view.GetPicks, now);
			j.Prop("ready", c.Legal || c.Accept);
			j.Prop("accept", c.Accept);
			j.Prop("message", c.Message);
			j.Prop("offered", c.Offered);
			j.Prop("asked", c.Asked);
			j.Prop("needed", (int)Math.Ceiling((double)c.Asked * c.Needed));
		}
		else
		{
			j.Prop("ready", value: false);
			j.Prop("accept", value: false);
			j.Prop("message", "");
			j.Prop("offered", 0);
			j.Prop("asked", 0);
			j.Prop("needed", 0);
		}
		j.EndObject();
		j.EndObject();
	}

	private static void WriteOfferAssets(JsonBuilder j, string name, SportsWorld world, League league, Team viewer, List<int> players, List<int> picks, out int total)
	{
		total = 0;
		int year = world.CurrentYear();
		j.Name(name).BeginArray();
		foreach (int id in players)
		{
			Player p = world.GetPlayer(id);
			if (p != null)
			{
				int v = world.TradePoints(viewer, p);
				total += v;
				j.BeginObject().Prop("id", p.Id).Prop("label", Rosters.PositionLabel(p.Position) + " " + p.Name + ", " + Rosters.Ovr(p) + " OVR, age " + (year - p.BirthYear) + ((p.ContractYears > 0) ? (", " + Economy.ContractText(p)) : ""))
					.Prop("value", v)
					.Prop("injury", world.InjuryText(p))
					.EndObject();
			}
		}
		foreach (int id2 in picks)
		{
			PickAsset a = world.FindPick(league, id2);
			if (a != null)
			{
				int v2 = world.TradePoints(viewer, a);
				total += v2;
				j.BeginObject().Prop("id", a.Id).Prop("label", world.PickLabel(league, a) + " (" + world.PickProjection(league, a).ToLowerInvariant() + ")")
					.Prop("value", v2)
					.Prop("injury", "")
					.EndObject();
			}
		}
		j.EndArray();
	}

	private static void WriteOffer(JsonBuilder j, SportsWorld world, League league, Team mine, TradeOffer o)
	{
		Team from = world.GetTeam(o.FromTeamId);
		if (from == null)
		{
			return;
		}
		j.BeginObject().Prop("id", o.Id).Prop("teamId", from.Id)
			.Prop("team", world.TeamName(from.Id))
			.Prop("mode", world.IsContending(from) ? "Contending" : "Rebuilding")
			.Prop("rival", world.AreRivals(league, mine.Id, from.Id));
		WriteOfferAssets(j, "youGet", world, league, mine, o.Give, o.GivePicks, out var get);
		WriteOfferAssets(j, "youGive", world, league, mine, o.Get, new List<int>(), out var give);
		j.Prop("getValue", get).Prop("giveValue", give);
		j.Name("give").BeginArray();
		foreach (int id in o.Get)
		{
			j.Value(id);
		}
		j.EndArray();
		j.Name("get").BeginArray();
		foreach (int id2 in o.Give)
		{
			j.Value(id2);
		}
		j.EndArray();
		j.Name("getPicks").BeginArray();
		foreach (int id3 in o.GivePicks)
		{
			j.Value(id3);
		}
		j.EndArray();
		j.EndObject();
	}

	private static void WriteFinder(JsonBuilder j, SportsWorld world, Team t, League league, int now, PanelView view)
	{
		int year = world.CurrentYear();
		List<Player> list = world.SignedPlayers(t.Id);
		list.Sort((Player a, Player b) => (b.Overall == a.Overall) ? a.Id.CompareTo(b.Id) : b.Overall.CompareTo(a.Overall));
		j.Name("shopList").BeginArray();
		foreach (Player p in list)
		{
			int age = year - p.BirthYear;
			j.BeginObject().Prop("id", p.Id).Prop("name", p.Name)
				.Prop("pos", Rosters.PositionLabel(p.Position))
				.Prop("age", age.ToString())
				.Prop("ovr", Rosters.Ovr(p))
				.Prop("pot", Rosters.Grade(p, age))
				.Prop("contract", Economy.ContractText(p))
				.Prop("reserve", p.Status == PlayerStatus.Prospect)
				.Prop("injury", world.InjuryText(p))
				.Prop("value", world.TradePoints(t, p))
				.Prop("onBlock", t.Block.Contains(p.Id))
				.EndObject();
		}
		j.EndArray();
		List<Player> shop = new List<Player>();
		foreach (int id in view.Shop)
		{
			Player p2 = world.GetPlayer(id);
			if (p2 != null && p2.TeamId == t.Id && shop.Count < 3)
			{
				shop.Add(p2);
			}
		}
		j.Name("shop").BeginArray();
		foreach (Player p3 in shop)
		{
			j.Value(p3.Id);
		}
		j.EndArray();
		j.Prop("want", view.Want ?? "");
		j.Name("found").BeginArray();
		if (shop.Count > 0 && world.TradeWindowOpen(league, now) && t.Manual)
		{
			List<TradeOffer> offers = world.FindOffers(t, shop, view.Want ?? "", now);
			for (int i = 0; i < offers.Count && i < 10; i++)
			{
				WriteOffer(j, world, league, t, offers[i]);
			}
		}
		j.EndArray();
	}

	private static void WriteBlock(JsonBuilder j, SportsWorld world, Team t, League league, int now)
	{
		int year = world.CurrentYear();
		j.Name("incoming").BeginArray();
		foreach (TradeOffer o in world.OffersFor(t.Id))
		{
			WriteOffer(j, world, league, t, o);
		}
		j.EndArray();
		j.Name("myBlock").BeginArray();
		foreach (BlockEntry e in world.TradeBlockOf(t))
		{
			WriteTradePlayer(j, world, t, t, e.Player, year);
		}
		j.EndArray();
		j.Name("available").BeginArray();
		foreach (int id in league.TeamIds)
		{
			Team other = world.GetTeam(id);
			if (other == null || other == t || other.Retired || (other.IsUser && other.Manual))
			{
				continue;
			}
			foreach (BlockEntry e2 in world.TradeBlockOf(other))
			{
				Player p = e2.Player;
				int age = year - p.BirthYear;
				j.BeginObject().Prop("id", p.Id).Prop("teamId", other.Id)
					.Prop("team", world.TeamName(other.Id))
					.Prop("name", p.Name)
					.Prop("pos", Rosters.PositionLabel(p.Position))
					.Prop("age", age.ToString())
					.Prop("ovr", Rosters.Ovr(p))
					.Prop("pot", Rosters.Grade(p, age))
					.Prop("contract", Economy.ContractText(p))
					.Prop("injury", world.InjuryText(p))
					.Prop("reason", e2.Reason)
					.Prop("value", world.TradePoints(t, p))
					.EndObject();
			}
		}
		j.EndArray();
	}

	private static void WriteTradePlayer(JsonBuilder j, SportsWorld world, Team owner, Team viewer, Player p, int year)
	{
		int age = year - p.BirthYear;
		j.BeginObject().Prop("id", p.Id).Prop("name", p.Name)
			.Prop("pos", Rosters.PositionLabel(p.Position))
			.Prop("age", age.ToString())
			.Prop("ovr", Rosters.Ovr(p))
			.Prop("pot", Rosters.Grade(p, age))
			.Prop("contract", Economy.ContractText(p))
			.Prop("reserve", p.Status == PlayerStatus.Prospect)
			.Prop("injury", world.InjuryText(p))
			.Prop("value", world.TradePoints(viewer, p))
			.EndObject();
	}

	private static void WriteTradePick(JsonBuilder j, SportsWorld world, League league, Team viewer, PickAsset a)
	{
		j.BeginObject().Prop("id", a.Id).Prop("label", world.PickLabel(league, a))
			.Prop("projection", world.PickProjection(league, a))
			.Prop("value", world.TradePoints(viewer, a))
			.EndObject();
	}

	private static string RivalryTag(SportsWorld world, League league, Match g)
	{
		return RivalTagText(world.FindRivalry(league, g.HomeTeamId, g.AwayTeamId, create: false), game: false);
	}

	private static void WriteTeamSchedule(JsonBuilder j, SportsWorld world, Team t, League league)
	{
		List<Match> results = new List<Match>();
		List<Match> coming = new List<Match>();
		if (league != null && league.Current != null)
		{
			foreach (Match g in league.Current.Games)
			{
				if (g.Involves(t.Id))
				{
					if (g.Played)
					{
						results.Add(g);
					}
					else
					{
						coming.Add(g);
					}
				}
			}
		}
		results.Sort((Match a, Match b) => b.Time.CompareTo(a.Time));
		coming.Sort((Match a, Match b) => a.Time.CompareTo(b.Time));
		j.Name("results").BeginArray();
		for (int i = 0; i < results.Count && i < 12; i++)
		{
			Match g2 = results[i];
			j.BeginObject().Prop("id", g2.Id).Prop("date", GameClock.ShortDayLabel(GameClock.DayOf(g2.Time)))
				.Prop("text", LastGameText(world, t.Id, g2))
				.Prop("rival", RivalryTag(world, league, g2))
				.Prop("stars", g2.Summary ?? "")
				.Prop("featured", g2.Featured)
				.EndObject();
		}
		j.EndArray();
		j.Name("schedule").BeginArray();
		for (int i2 = 0; i2 < coming.Count && i2 < 12; i2++)
		{
			Match g3 = coming[i2];
			LiveSnapshot live = Live(league.Sport, league.Level, g3, world.LastTime);
			j.BeginObject().Prop("id", g3.Id).Prop("date", (live != null) ? "LIVE" : GameClock.TimeLabel(g3.Time))
				.Prop("live", live != null)
				.Prop("text", (live != null) ? (world.TeamAbbrev(g3.AwayTeamId) + " " + live.AwayScore + ", " + world.TeamAbbrev(g3.HomeTeamId) + " " + live.HomeScore + "  ·  " + live.Status) : NextGameText(world, league, t.Id, g3).Substring(GameClock.TimeLabel(g3.Time).Length).Trim())
				.Prop("rival", RivalryTag(world, league, g3))
				.Prop("featured", g3.Featured)
				.EndObject();
		}
		j.EndArray();
	}

	private static void WriteTransfers(JsonBuilder j, SportsWorld world, Team t, League league, int now)
	{
		int year = world.CurrentYear();
		bool open = SportsWorld.TransferWindowOpen(now);
		int budget = world.TransferBudgetOf(t);
		j.Name("transfers").BeginObject();
		j.Prop("open", open);
		j.Prop("window", SportsWorld.TransferWindowText(now));
		j.Prop("budget", Economy.Money(budget));
		j.Prop("budgetK", budget);
		j.Prop("manual", t.Manual);
		j.Prop("note", t.Manual ? "Bid the asking price and the club says yes. Offer about 10% less and it's a coin flip. The player then signs with you, so he has to fit your salary budget and roster. Fees you receive go into your transfer budget, and every new league year adds more (more for a good season)." : "The AI sporting director buys and sells players for this club. Choose \"Run this team myself\" to make transfers.");
		j.Name("offers").BeginArray();
		foreach (TransferOffer o in world.TransferOffers)
		{
			if (o.SellerTeamId == t.Id)
			{
				Player p = world.GetPlayer(o.PlayerId);
				if (p != null)
				{
					j.BeginObject().Prop("id", o.Id).Prop("playerId", p.Id)
						.Prop("player", Rosters.PositionLabel(p.Position) + " " + p.Name + ", " + Rosters.Ovr(p) + " OVR, age " + (year - p.BirthYear))
						.Prop("buyer", (o.BuyerTeamId < 0) ? o.BuyerName : ("the " + o.BuyerName))
						.Prop("abroad", o.BuyerTeamId < 0)
						.Prop("fee", Economy.Money(o.Fee))
						.Prop("value", Economy.Money(world.TransferValue(p)))
						.Prop("listed", t.Block.Contains(p.Id))
						.EndObject();
				}
			}
		}
		j.EndArray();
		j.Name("squad").BeginArray();
		List<Player> list = world.SignedPlayers(t.Id);
		list.Sort((Player a, Player b) => (b.Overall == a.Overall) ? a.Id.CompareTo(b.Id) : b.Overall.CompareTo(a.Overall));
		foreach (Player p2 in list)
		{
			int age = year - p2.BirthYear;
			j.BeginObject().Prop("id", p2.Id).Prop("name", p2.Name)
				.Prop("pos", Rosters.PositionLabel(p2.Position))
				.Prop("age", age.ToString())
				.Prop("ovr", Rosters.Ovr(p2))
				.Prop("pot", Rosters.Grade(p2, age))
				.Prop("contract", Economy.ContractText(p2))
				.Prop("value", Economy.Money(world.TransferValue(p2)))
				.Prop("listed", t.Block.Contains(p2.Id))
				.Prop("injury", world.InjuryText(p2))
				.EndObject();
		}
		j.EndArray();
		List<Player> market = world.TransferTargets(t);
		market.Sort((Player a, Player b) => (b.Overall == a.Overall) ? a.Id.CompareTo(b.Id) : b.Overall.CompareTo(a.Overall));
		List<Player> shown = new List<Player>();
		foreach (Player p3 in market)
		{
			if (p3.Status == PlayerStatus.Abroad || shown.Count < 160)
			{
				shown.Add(p3);
			}
		}
		j.Name("market").BeginArray();
		foreach (Player p4 in shown)
		{
			int age2 = year - p4.BirthYear;
			int asking = world.AskingFee(p4);
			int low = Math.Max(50, (int)Math.Round((double)asking * 0.9 / 50.0) * 50);
			bool abroad = p4.Status == PlayerStatus.Abroad;
			Team owner = (abroad ? null : world.GetTeam(p4.TeamId));
			string why = "";
			if (!t.Manual)
			{
				why = "Run this team yourself to make transfers.";
			}
			else if (!open)
			{
				why = SportsWorld.TransferWindowText(now);
			}
			else if (owner != null && owner.IsUser && owner.Manual)
			{
				why = "You run both clubs.";
			}
			else if (low > budget)
			{
				why = "More than your transfer budget.";
			}
			JsonBuilder jsonBuilder = j.BeginObject().Prop("id", p4.Id).Prop("name", p4.Name)
				.Prop("pos", Rosters.PositionLabel(p4.Position))
				.Prop("age", age2.ToString())
				.Prop("ovr", Rosters.Ovr(p4))
				.Prop("pot", Rosters.Grade(p4, age2));
			string value;
			if (abroad)
			{
				value = (string.IsNullOrEmpty(p4.From) ? "Abroad" : p4.From);
			}
			else
			{
				value = world.TeamName(p4.TeamId);
			}
			JsonBuilder jsonBuilder2 = jsonBuilder.Prop("club", value).Prop("teamId", abroad ? (-1) : p4.TeamId).Prop("abroad", abroad)
				.Prop("contract", abroad ? "" : Economy.ContractText(p4))
				.Prop("value", Economy.Money(world.TransferValue(p4)))
				.Prop("asking", Economy.Money(asking))
				.Prop("askingK", asking)
				.Prop("low", Economy.Money(low))
				.Prop("lowK", low)
				.Prop("canPay", why.Length == 0 && asking <= budget)
				.Prop("canLow", why.Length == 0);
			string value2;
			if (why.Length > 0)
			{
				value2 = why;
			}
			else
			{
				value2 = ((asking > budget) ? "The asking price is more than your transfer budget." : "");
			}
			jsonBuilder2.Prop("why", value2).Prop("injury", world.InjuryText(p4)).EndObject();
		}
		j.EndArray();
		j.Prop("marketTotal", market.Count);
		j.Name("recent").BeginArray();
		for (int i = 0; i < league.Transfers.Count && i < 20; i++)
		{
			j.Value(league.Transfers[i]);
		}
		j.EndArray();
		j.EndObject();
	}
}
