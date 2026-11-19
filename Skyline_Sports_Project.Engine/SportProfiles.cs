using System;
using System.Collections.Generic;

namespace Skyline_Sports_Project.Engine;

public static class SportProfiles
{
	private static readonly Dictionary<string, SportProfile> _profiles = Build();

	private const DayOfWeek Mon = DayOfWeek.Monday;

	private const DayOfWeek Tue = DayOfWeek.Tuesday;

	private const DayOfWeek Wed = DayOfWeek.Wednesday;

	private const DayOfWeek Thu = DayOfWeek.Thursday;

	private const DayOfWeek Fri = DayOfWeek.Friday;

	private const DayOfWeek Sat = DayOfWeek.Saturday;

	private const DayOfWeek Sun = DayOfWeek.Sunday;

	public static SportProfile Get(Sport sport, Level level)
	{
		if (_profiles.TryGetValue(sport.ToString() + "-" + level, out var p))
		{
			return p;
		}
		return _profiles[Sport.Basketball.ToString() + "-" + Level.Pro];
	}

	private static int M(int hhmm)
	{
		return hhmm / 100 * 60 + hhmm % 100;
	}

	private static TimeOption T(int hhmm, double weight)
	{
		return new TimeOption(0, M(hhmm), weight, 0);
	}

	private static TimeOption T(int dayOffset, int hhmm, double weight, int cap)
	{
		return new TimeOption(dayOffset, M(hhmm), weight, cap);
	}

	private static DayOfWeek[] Days(params DayOfWeek[] d)
	{
		return d;
	}

	private static TimeOption[] Spread(params int[] hhmm)
	{
		TimeOption[] list = new TimeOption[hhmm.Length];
		for (int i = 0; i < hhmm.Length; i++)
		{
			list[i] = new TimeOption(0, M(hhmm[i]), 1.0, 1);
		}
		return list;
	}

	private static bool Weekend(DayOfWeek d)
	{
		if (d != DayOfWeek.Saturday)
		{
			return d == DayOfWeek.Sunday;
		}
		return true;
	}

	private static PlayoffRoundFormat Series7(string name, Func<DayOfWeek, TimeOption[]> times, int rest)
	{
		PlayoffRoundFormat playoffRoundFormat = new PlayoffRoundFormat();
		playoffRoundFormat.Name = name;
		playoffRoundFormat.BestOf = 7;
		playoffRoundFormat.DayOffsets = new int[7] { 0, 2, 4, 6, 8, 10, 12 };
		playoffRoundFormat.HighSeedHome = new bool[7] { true, true, false, false, true, false, true };
		playoffRoundFormat.RestDays = rest;
		playoffRoundFormat.Times = times;
		playoffRoundFormat.Stagger = 1;
		return playoffRoundFormat;
	}

	private static Dictionary<string, SportProfile> Build()
	{
		List<SportProfile> list = new List<SportProfile>();
		Func<DayOfWeek, TimeOption[]> proHockeyPlayoffTimes = (DayOfWeek d) => Weekend(d) ? new TimeOption[3]
		{
			T(1500, 2.0),
			T(1900, 3.0),
			T(2000, 2.0)
		} : new TimeOption[3]
		{
			T(1900, 4.0),
			T(1930, 3.0),
			T(2000, 2.0)
		};
		list.Add(new SportProfile
		{
			Sport = Sport.Hockey,
			Level = Level.Pro,
			Summary = "84 games, late September to early April, about 3 or 4 games a week, mostly 7 PM starts, with a holiday break and an All-Star break. The top half makes the playoffs, best-of-7 every round (2-2-1-1-1 home ice).",
			GamesPerTeam = 84,
			Start = (int y) => GameClock.OnOrAfter(GameClock.D(y, 9, 28), DayOfWeek.Tuesday),
			End = (int y) => GameClock.OnOrAfter(GameClock.D(y + 1, 4, 9), DayOfWeek.Saturday),
			Breaks = (int y) => new DateRange[2]
			{
				new DateRange(GameClock.D(y, 12, 24), GameClock.D(y, 12, 26)),
				new DateRange(GameClock.Nth(y + 1, 2, DayOfWeek.Sunday, 1).AddDays(-3.0), GameClock.Nth(y + 1, 2, DayOfWeek.Sunday, 1).AddDays(2.0))
			},
			Week = new SlotTemplate[4]
			{
				new SlotTemplate
				{
					Anchor = DayOfWeek.Tuesday,
					Times = (DayOfWeek d, bool last) => new TimeOption[5]
					{
						T(0, 1900, 5.0, 0),
						T(0, 1930, 3.0, 0),
						T(0, 2000, 1.0, 0),
						T(1, 1900, 3.0, 0),
						T(1, 1930, 2.0, 0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Thursday,
					Times = (DayOfWeek d, bool last) => new TimeOption[5]
					{
						T(0, 1900, 5.0, 0),
						T(0, 1930, 3.0, 0),
						T(0, 2000, 1.0, 0),
						T(1, 1900, 2.0, 0),
						T(1, 1930, 1.0, 0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Saturday,
					Times = (DayOfWeek d, bool last) => new TimeOption[4]
					{
						T(0, 1300, 1.0, 0),
						T(0, 1600, 1.0, 0),
						T(0, 1900, 5.0, 0),
						T(0, 2000, 1.0, 0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Sunday,
					Optional = true,
					Times = (DayOfWeek d, bool last) => new TimeOption[4]
					{
						T(0, 1230, 2.0, 0),
						T(0, 1500, 2.0, 0),
						T(0, 1700, 1.0, 0),
						T(0, 1900, 1.0, 0)
					}
				}
			},
			PlayoffShare = 0.5,
			MaxPlayoffTeams = 16,
			PlayoffStart = (int y) => GameClock.OnOrAfter(GameClock.D(y + 1, 4, 9), DayOfWeek.Saturday).AddDays(3.0),
			RoundsFromFinal = new PlayoffRoundFormat[4]
			{
				Series7("Cup Final", proHockeyPlayoffTimes, 3),
				Series7("Conference Final", proHockeyPlayoffTimes, 2),
				Series7("Second Round", proHockeyPlayoffTimes, 2),
				Series7("First Round", proHockeyPlayoffTimes, 2)
			}
		});
		Func<DayOfWeek, TimeOption[]> proHoopsPlayoffTimes = (DayOfWeek d) => Weekend(d) ? new TimeOption[4]
		{
			T(1300, 1.0),
			T(1530, 2.0),
			T(1800, 1.0),
			T(2030, 2.0)
		} : new TimeOption[4]
		{
			T(1900, 2.0),
			T(1930, 3.0),
			T(2000, 2.0),
			T(2130, 1.0)
		};
		list.Add(new SportProfile
		{
			Sport = Sport.Basketball,
			Level = Level.Pro,
			Summary = "82 games, late October to mid-April, about 3 or 4 games a week, mostly 7 to 8 PM tips. Playoffs start after the Play-In week: the top half, best-of-7 every round.",
			GamesPerTeam = 82,
			Start = (int y) => GameClock.OnOrAfter(GameClock.D(y, 10, 20), DayOfWeek.Tuesday),
			End = (int y) => GameClock.Nth(y + 1, 4, DayOfWeek.Sunday, 2),
			Breaks = (int y) => new DateRange[3]
			{
				new DateRange(GameClock.Nth(y, 11, DayOfWeek.Thursday, 4), GameClock.Nth(y, 11, DayOfWeek.Thursday, 4)),
				new DateRange(GameClock.D(y, 12, 24), GameClock.D(y, 12, 24)),
				new DateRange(GameClock.Nth(y + 1, 2, DayOfWeek.Sunday, 3).AddDays(-3.0), GameClock.Nth(y + 1, 2, DayOfWeek.Sunday, 3).AddDays(3.0))
			},
			Week = new SlotTemplate[4]
			{
				new SlotTemplate
				{
					Anchor = DayOfWeek.Monday,
					Times = (DayOfWeek d, bool last) => new TimeOption[6]
					{
						T(0, 1900, 3.0, 0),
						T(0, 1930, 3.0, 0),
						T(0, 2000, 1.0, 0),
						T(1, 1900, 3.0, 0),
						T(1, 1930, 3.0, 0),
						T(1, 2200, 1.0, 0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Wednesday,
					Times = (DayOfWeek d, bool last) => new TimeOption[6]
					{
						T(0, 1900, 3.0, 0),
						T(0, 1930, 3.0, 0),
						T(0, 2000, 1.0, 0),
						T(1, 1930, 3.0, 0),
						T(1, 2000, 2.0, 0),
						T(1, 2200, 1.0, 0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Friday,
					Times = (DayOfWeek d, bool last) => new TimeOption[6]
					{
						T(0, 1900, 3.0, 0),
						T(0, 1930, 3.0, 0),
						T(0, 2000, 2.0, 0),
						T(1, 1700, 1.0, 0),
						T(1, 1930, 2.0, 0),
						T(1, 2030, 1.0, 0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Sunday,
					Optional = true,
					Times = (DayOfWeek d, bool last) => new TimeOption[5]
					{
						T(0, 1230, 1.0, 0),
						T(0, 1500, 2.0, 0),
						T(0, 1530, 1.0, 0),
						T(0, 1800, 2.0, 0),
						T(0, 1900, 1.0, 0)
					}
				}
			},
			PlayoffShare = 0.5,
			MaxPlayoffTeams = 16,
			PlayoffStart = (int y) => GameClock.Nth(y + 1, 4, DayOfWeek.Sunday, 2).AddDays(6.0),
			RoundsFromFinal = new PlayoffRoundFormat[4]
			{
				Series7("Finals", proHoopsPlayoffTimes, 3),
				Series7("Conference Finals", proHoopsPlayoffTimes, 2),
				Series7("Conference Semifinals", proHoopsPlayoffTimes, 2),
				Series7("First Round", proHoopsPlayoffTimes, 6)
			}
		});
		list.Add(new SportProfile
		{
			Sport = Sport.Football,
			Level = Level.Pro,
			Summary = "17 games over 18 weeks with one bye, September to early January. Thursday night, Sunday afternoon and night, Monday night. Single-game playoffs, then the title game at a neutral site in February.",
			GamesPerTeam = 17,
			ByesPerTeam = 1,
			Weekly = true,
			Start = (int y) => GameClock.Nth(y, 9, DayOfWeek.Monday, 1).AddDays(3.0),
			End = (int y) => GameClock.Nth(y, 9, DayOfWeek.Monday, 1).AddDays(125.0),
			Week = new SlotTemplate[1]
			{
				new SlotTemplate
				{
					Anchor = DayOfWeek.Sunday,
					Times = (DayOfWeek d, bool last) => new TimeOption[6]
					{
						T(-3, 2015, 1.0, 1),
						T(0, 1300, 9.0, 0),
						T(0, 1605, 2.0, 2),
						T(0, 1625, 3.0, 3),
						T(0, 2020, 1.0, 1),
						T(1, 2015, 1.0, 1)
					}
				}
			},
			PlayoffShare = 0.4375,
			PlayoffPowerOfTwo = false,
			MaxPlayoffTeams = 14,
			SingleYearLabel = true,
			ChampionLabel = "Champions",
			RoundsFromFinal = new PlayoffRoundFormat[4]
			{
				new PlayoffRoundFormat
				{
					Name = "Championship Game",
					Neutral = true,
					FixedDate = (int y) => GameClock.Nth(y + 1, 2, DayOfWeek.Sunday, 2),
					StartDays = Days(default(DayOfWeek)),
					RestDays = 13,
					Times = (DayOfWeek d) => new TimeOption[1] { T(1830, 1.0) }
				},
				new PlayoffRoundFormat
				{
					Name = "Conference Championship",
					FixedDate = (int y) => GameClock.Nth(y + 1, 2, DayOfWeek.Sunday, 2).AddDays(-14.0),
					StartDays = Days(default(DayOfWeek)),
					RestDays = 6,
					Times = (DayOfWeek d) => new TimeOption[2]
					{
						T(0, 1500, 1.0, 1),
						T(0, 1830, 1.0, 1)
					}
				},
				new PlayoffRoundFormat
				{
					Name = "Divisional Round",
					FixedDate = (int y) => GameClock.Nth(y + 1, 2, DayOfWeek.Sunday, 2).AddDays(-22.0),
					StartDays = Days(DayOfWeek.Saturday),
					RestDays = 4,
					Times = (DayOfWeek d) => new TimeOption[4]
					{
						T(0, 1630, 1.0, 1),
						T(0, 2015, 1.0, 1),
						T(1, 1500, 1.0, 1),
						T(1, 1830, 1.0, 1)
					}
				},
				new PlayoffRoundFormat
				{
					Name = "Wild Card",
					FixedDate = (int y) => GameClock.Nth(y + 1, 2, DayOfWeek.Sunday, 2).AddDays(-29.0),
					StartDays = Days(DayOfWeek.Saturday),
					RestDays = 5,
					Times = (DayOfWeek d) => new TimeOption[6]
					{
						T(0, 1630, 1.0, 1),
						T(0, 2015, 1.0, 1),
						T(1, 1300, 1.0, 1),
						T(1, 1630, 1.0, 1),
						T(1, 2015, 1.0, 1),
						T(2, 2015, 1.0, 1)
					}
				}
			}
		});
		Func<DayOfWeek, bool, TimeOption[]> mlbTimes = (DayOfWeek d, bool last) =>
		{
			switch (d)
			{
			case DayOfWeek.Saturday:
				return new TimeOption[5]
				{
					T(1305, 2.0),
					T(1605, 3.0),
					T(1815, 1.0),
					T(1910, 2.0),
					T(1915, 1.0)
				};
			case DayOfWeek.Sunday:
				return new TimeOption[5]
				{
					T(1305, 3.0),
					T(1310, 2.0),
					T(1335, 3.0),
					T(1610, 1.0),
					T(1910, 0.3)
				};
			case DayOfWeek.Friday:
				return new TimeOption[4]
				{
					T(1840, 2.0),
					T(1905, 3.0),
					T(1910, 2.0),
					T(2010, 1.0)
				};
			default:
				if (last)
				{
					return new TimeOption[4]
					{
						T(1235, 1.0),
						T(1305, 2.0),
						T(1840, 2.0),
						T(1905, 3.0)
					};
				}
				return new TimeOption[4]
				{
					T(1840, 2.0),
					T(1905, 4.0),
					T(1910, 2.0),
					T(1820, 1.0)
				};
			}
		};
		Func<DayOfWeek, TimeOption[]> mlbPlayoffTimes = (DayOfWeek d) => Weekend(d) ? new TimeOption[3]
		{
			T(1308, 1.0),
			T(1608, 2.0),
			T(2008, 2.0)
		} : new TimeOption[3]
		{
			T(1608, 1.0),
			T(1808, 2.0),
			T(2008, 3.0)
		};
		list.Add(new SportProfile
		{
			Sport = Sport.Baseball,
			Level = Level.Pro,
			Summary = "162 games, late March to late September, mostly 3-game series with off days and an All-Star break in July. October postseason: best-of-3 Wild Card, best-of-5 Division Series, best-of-7 LCS and World Series (smaller leagues start at a later round).",
			GamesPerTeam = 162,
			Start = (int y) => GameClock.Last(y, 3, DayOfWeek.Thursday),
			End = (int y) => GameClock.Last(y, 9, DayOfWeek.Sunday),
			Breaks = (int y) => new DateRange[1]
			{
				new DateRange(GameClock.Nth(y, 7, DayOfWeek.Tuesday, 2).AddDays(-1.0), GameClock.Nth(y, 7, DayOfWeek.Tuesday, 2).AddDays(2.0))
			},
			Week = new SlotTemplate[3]
			{
				new SlotTemplate
				{
					Anchor = DayOfWeek.Monday,
					Optional = true,
					Times = (DayOfWeek d, bool last) => new TimeOption[3]
					{
						T(1840, 2.0),
						T(1905, 2.0),
						T(1910, 1.0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Tuesday,
					Length = 3,
					Times = mlbTimes
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Friday,
					Length = 3,
					Times = mlbTimes
				}
			},
			PlayoffShare = 0.4,
			PlayoffPowerOfTwo = false,
			MinPlayoffTeams = 4,
			MaxPlayoffTeams = 12,
			RoundsFromFinal = new PlayoffRoundFormat[4]
			{
				new PlayoffRoundFormat
				{
					Name = "World Series",
					BestOf = 7,
					DayOffsets = new int[7] { 0, 1, 3, 4, 5, 7, 8 },
					HighSeedHome = new bool[7] { true, true, false, false, false, true, true },
					RestDays = 3,
					Times = (DayOfWeek d) => new TimeOption[1] { T(2003, 1.0) }
				},
				new PlayoffRoundFormat
				{
					Name = "League Championship Series",
					BestOf = 7,
					DayOffsets = new int[7] { 0, 1, 3, 4, 5, 7, 8 },
					HighSeedHome = new bool[7] { true, true, false, false, false, true, true },
					RestDays = 1,
					Times = mlbPlayoffTimes
				},
				new PlayoffRoundFormat
				{
					Name = "Division Series",
					BestOf = 5,
					DayOffsets = new int[5] { 0, 1, 3, 4, 6 },
					HighSeedHome = new bool[5] { true, true, false, false, true },
					RestDays = 1,
					Times = mlbPlayoffTimes
				},
				new PlayoffRoundFormat
				{
					Name = "Wild Card Series",
					BestOf = 3,
					DayOffsets = new int[3] { 0, 1, 2 },
					HighSeedHome = new bool[3] { true, true, true },
					RestDays = 1,
					Times = mlbPlayoffTimes
				}
			}
		});
		Func<DayOfWeek, TimeOption[]> mlsPlayoffTimes = (DayOfWeek d) => Weekend(d) ? new TimeOption[4]
		{
			T(1530, 1.0),
			T(1800, 1.0),
			T(1930, 2.0),
			T(2030, 1.0)
		} : new TimeOption[2]
		{
			T(1930, 3.0),
			T(2000, 1.0)
		};
		list.Add(new SportProfile
		{
			Sport = Sport.Soccer,
			Level = Level.Pro,
			Summary = "34 matches on the new summer-to-spring calendar: mid-July to mid-December, a winter break, then February to late April, plus a few midweek rounds. Knockout playoffs in May, Cup Final on the last Saturday of May.",
			GamesPerTeam = 34,
			Weekly = true,
			Start = (int y) => GameClock.Nth(y, 7, DayOfWeek.Saturday, 3),
			End = (int y) => GameClock.Last(y + 1, 4, DayOfWeek.Sunday),
			Breaks = (int y) => new DateRange[4]
			{
				new DateRange(GameClock.Nth(y, 12, DayOfWeek.Monday, 2), GameClock.Nth(y + 1, 2, DayOfWeek.Friday, 2)),
				new DateRange(GameClock.Nth(y, 10, DayOfWeek.Saturday, 2).AddDays(-3.0), GameClock.Nth(y, 10, DayOfWeek.Saturday, 2).AddDays(3.0)),
				new DateRange(GameClock.Nth(y, 11, DayOfWeek.Saturday, 2).AddDays(-3.0), GameClock.Nth(y, 11, DayOfWeek.Saturday, 2).AddDays(3.0)),
				new DateRange(GameClock.Nth(y + 1, 3, DayOfWeek.Saturday, 4).AddDays(-3.0), GameClock.Nth(y + 1, 3, DayOfWeek.Saturday, 4).AddDays(3.0))
			},
			Week = new SlotTemplate[2]
			{
				new SlotTemplate
				{
					Anchor = DayOfWeek.Wednesday,
					Optional = true,
					Times = (DayOfWeek d, bool last) => new TimeOption[2]
					{
						T(1930, 4.0),
						T(2000, 1.0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Saturday,
					Times = (DayOfWeek d, bool last) => new TimeOption[7]
					{
						T(0, 1930, 6.0, 0),
						T(0, 1900, 1.0, 0),
						T(0, 2030, 1.0, 0),
						T(0, 1630, 1.0, 0),
						T(1, 1300, 1.0, 1),
						T(1, 1600, 1.0, 1),
						T(1, 1800, 1.0, 1)
					}
				}
			},
			PlayoffShare = 0.6,
			MaxPlayoffTeams = 16,
			RoundsFromFinal = new PlayoffRoundFormat[4]
			{
				new PlayoffRoundFormat
				{
					Name = "Cup Final",
					FixedDate = (int y) => GameClock.Last(y + 1, 5, DayOfWeek.Saturday),
					StartDays = Days(DayOfWeek.Saturday),
					RestDays = 6,
					Times = (DayOfWeek d) => new TimeOption[1] { T(2000, 1.0) }
				},
				new PlayoffRoundFormat
				{
					Name = "Conference Final",
					StartDays = Days(DayOfWeek.Saturday, DayOfWeek.Sunday),
					RestDays = 5,
					Times = mlsPlayoffTimes
				},
				new PlayoffRoundFormat
				{
					Name = "Conference Semifinal",
					StartDays = Days(DayOfWeek.Saturday, DayOfWeek.Sunday),
					RestDays = 5,
					Times = mlsPlayoffTimes
				},
				new PlayoffRoundFormat
				{
					Name = "Round One",
					BestOf = 3,
					DayOffsets = new int[3] { 0, 5, 9 },
					HighSeedHome = new bool[3] { true, false, true },
					StartDays = Days(DayOfWeek.Friday, DayOfWeek.Saturday),
					RestDays = 4,
					Times = mlsPlayoffTimes
				}
			}
		});
		list.Add(new SportProfile
		{
			Sport = Sport.Soccer,
			Level = Level.Minor,
			Summary = "34 matches on the same calendar as the top division: mid-July to mid-December, a winter break, then February to late April. The top of the table goes up, and the promotion playoff in May decides the last spot.",
			GamesPerTeam = 34,
			Weekly = true,
			Start = (int y) => GameClock.Nth(y, 7, DayOfWeek.Saturday, 3),
			End = (int y) => GameClock.Last(y + 1, 4, DayOfWeek.Sunday),
			Breaks = (int y) => new DateRange[4]
			{
				new DateRange(GameClock.Nth(y, 12, DayOfWeek.Monday, 2), GameClock.Nth(y + 1, 2, DayOfWeek.Friday, 2)),
				new DateRange(GameClock.Nth(y, 10, DayOfWeek.Saturday, 2).AddDays(-3.0), GameClock.Nth(y, 10, DayOfWeek.Saturday, 2).AddDays(3.0)),
				new DateRange(GameClock.Nth(y, 11, DayOfWeek.Saturday, 2).AddDays(-3.0), GameClock.Nth(y, 11, DayOfWeek.Saturday, 2).AddDays(3.0)),
				new DateRange(GameClock.Nth(y + 1, 3, DayOfWeek.Saturday, 4).AddDays(-3.0), GameClock.Nth(y + 1, 3, DayOfWeek.Saturday, 4).AddDays(3.0))
			},
			Week = new SlotTemplate[2]
			{
				new SlotTemplate
				{
					Anchor = DayOfWeek.Tuesday,
					Optional = true,
					Times = (DayOfWeek d, bool last) => new TimeOption[2]
					{
						T(1945, 4.0),
						T(1900, 1.0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Saturday,
					Times = (DayOfWeek d, bool last) => new TimeOption[5]
					{
						T(0, 1500, 6.0, 0),
						T(0, 1730, 2.0, 0),
						T(0, 1930, 1.0, 0),
						T(1, 1300, 1.0, 1),
						T(1, 1530, 1.0, 1)
					}
				}
			},
			PlayoffShare = 0.4,
			MaxPlayoffTeams = 8,
			RoundsFromFinal = new PlayoffRoundFormat[3]
			{
				new PlayoffRoundFormat
				{
					Name = "Playoff Final",
					FixedDate = (int y) => GameClock.Last(y + 1, 5, DayOfWeek.Saturday).AddDays(-7.0),
					StartDays = Days(DayOfWeek.Saturday),
					RestDays = 6,
					Times = (DayOfWeek d) => new TimeOption[1] { T(1530, 1.0) }
				},
				new PlayoffRoundFormat
				{
					Name = "Playoff Semifinal",
					StartDays = Days(DayOfWeek.Saturday, DayOfWeek.Sunday),
					RestDays = 5,
					Times = mlsPlayoffTimes
				},
				new PlayoffRoundFormat
				{
					Name = "Playoff Quarterfinal",
					StartDays = Days(DayOfWeek.Saturday, DayOfWeek.Sunday),
					RestDays = 4,
					Times = mlsPlayoffTimes
				}
			}
		});
		list.Add(new SportProfile
		{
			Sport = Sport.Football,
			Level = Level.College,
			Summary = "12 games over 14 weeks (two bye weeks), late August to the Saturday after Thanksgiving. Saturdays at noon, 3:30 and 7:30, some Thursday and Friday nights. Playoff in December and January, final on a Monday night at a neutral site.",
			GamesPerTeam = 12,
			ByesPerTeam = 2,
			Weekly = true,
			Start = (int y) => GameClock.Nth(y, 9, DayOfWeek.Monday, 1).AddDays(-9.0),
			End = (int y) => GameClock.Nth(y, 11, DayOfWeek.Thursday, 4).AddDays(2.0),
			Week = new SlotTemplate[1]
			{
				new SlotTemplate
				{
					Anchor = DayOfWeek.Saturday,
					Times = (DayOfWeek d, bool last) => new TimeOption[7]
					{
						T(-2, 1930, 0.5, 1),
						T(-1, 2000, 0.6, 1),
						T(0, 1200, 4.0, 0),
						T(0, 1530, 4.0, 0),
						T(0, 1900, 2.0, 0),
						T(0, 1930, 2.0, 0),
						T(0, 2230, 0.15, 1)
					}
				}
			},
			PlayoffShare = 0.5,
			MinPlayoffTeams = 2,
			MaxPlayoffTeams = 8,
			ChampionLabel = "National Champions",
			SingleYearLabel = true,
			RoundsFromFinal = new PlayoffRoundFormat[4]
			{
				new PlayoffRoundFormat
				{
					Name = "National Championship",
					Neutral = true,
					FixedDate = (int y) => GameClock.OnOrAfter(GameClock.D(y + 1, 1, 19), DayOfWeek.Monday),
					StartDays = Days(DayOfWeek.Monday),
					RestDays = 7,
					Times = (DayOfWeek d) => new TimeOption[1] { T(1930, 1.0) }
				},
				new PlayoffRoundFormat
				{
					Name = "Playoff Semifinal",
					Neutral = true,
					FixedDate = (int y) => GameClock.OnOrAfter(GameClock.D(y + 1, 1, 14), DayOfWeek.Thursday),
					StartDays = Days(DayOfWeek.Thursday),
					RestDays = 7,
					Times = (DayOfWeek d) => new TimeOption[2]
					{
						T(0, 1930, 1.0, 1),
						T(1, 1930, 1.0, 1)
					}
				},
				new PlayoffRoundFormat
				{
					Name = "Playoff Quarterfinal",
					Neutral = true,
					FixedDate = (int y) => GameClock.D(y, 12, 31),
					RestDays = 7,
					Times = (DayOfWeek d) => new TimeOption[4]
					{
						T(-1, 1930, 1.0, 1),
						T(0, 1200, 1.0, 1),
						T(0, 1930, 1.0, 1),
						T(1, 1300, 1.0, 1)
					}
				},
				new PlayoffRoundFormat
				{
					Name = "Playoff First Round",
					FixedDate = (int y) => GameClock.Nth(y, 12, DayOfWeek.Saturday, 3),
					StartDays = Days(DayOfWeek.Saturday),
					RestDays = 7,
					Times = (DayOfWeek d) => new TimeOption[4]
					{
						T(-1, 2000, 1.0, 1),
						T(0, 1200, 1.0, 1),
						T(0, 1530, 1.0, 1),
						T(0, 1930, 1.0, 1)
					}
				}
			}
		});
		list.Add(new SportProfile
		{
			Sport = Sport.Basketball,
			Level = Level.College,
			Summary = "31 games, early November to early March: weeknight games (7 and 9 PM) and Saturdays. Conference tournament in March at a neutral site, everyone qualifies, one game a day.",
			GamesPerTeam = 31,
			Start = (int y) => GameClock.Nth(y, 11, DayOfWeek.Monday, 1),
			End = (int y) => GameClock.OnOrAfter(GameClock.D(y + 1, 3, 1), DayOfWeek.Sunday),
			Breaks = (int y) => new DateRange[2]
			{
				new DateRange(GameClock.Nth(y, 11, DayOfWeek.Thursday, 4), GameClock.Nth(y, 11, DayOfWeek.Thursday, 4)),
				new DateRange(GameClock.D(y, 12, 22), GameClock.D(y, 12, 27))
			},
			Week = new SlotTemplate[3]
			{
				new SlotTemplate
				{
					Anchor = DayOfWeek.Tuesday,
					Times = (DayOfWeek d, bool last) => new TimeOption[7]
					{
						T(0, 1900, 3.0, 0),
						T(0, 2100, 2.0, 0),
						T(1, 1900, 3.0, 0),
						T(1, 2100, 2.0, 0),
						T(2, 1900, 2.0, 0),
						T(2, 2100, 1.0, 0),
						T(1, 1830, 1.0, 0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Saturday,
					Times = (DayOfWeek d, bool last) => new TimeOption[7]
					{
						T(0, 1200, 2.0, 0),
						T(0, 1400, 2.0, 0),
						T(0, 1600, 2.0, 0),
						T(0, 1800, 2.0, 0),
						T(0, 2000, 2.0, 0),
						T(1, 1300, 1.0, 0),
						T(1, 1800, 1.0, 0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Friday,
					Optional = true,
					Times = (DayOfWeek d, bool last) => new TimeOption[2]
					{
						T(1900, 2.0),
						T(2100, 1.0)
					}
				}
			},
			PlayoffShare = 1.0,
			PlayoffPowerOfTwo = false,
			MaxPlayoffTeams = 16,
			ChampionLabel = "Conference Tournament Champions",
			RoundsFromFinal = new PlayoffRoundFormat[4]
			{
				new PlayoffRoundFormat
				{
					Name = "Championship",
					Neutral = true,
					RestDays = 0,
					Times = (DayOfWeek d) => new TimeOption[1] { T(1800, 1.0) }
				},
				new PlayoffRoundFormat
				{
					Name = "Semifinals",
					Neutral = true,
					RestDays = 0,
					Times = (DayOfWeek d) => Spread(1900, 2130)
				},
				new PlayoffRoundFormat
				{
					Name = "Quarterfinals",
					Neutral = true,
					FixedDate = (int y) => GameClock.OnOrAfter(GameClock.D(y + 1, 3, 10), DayOfWeek.Thursday),
					RestDays = 0,
					Times = (DayOfWeek d) => Spread(1200, 1430, 1900, 2130)
				},
				new PlayoffRoundFormat
				{
					Name = "First Round",
					Neutral = true,
					FixedDate = (int y) => GameClock.OnOrAfter(GameClock.D(y + 1, 3, 9), DayOfWeek.Wednesday),
					RestDays = 2,
					Times = (DayOfWeek d) => Spread(1200, 1430, 1900, 2130)
				}
			}
		});
		list.Add(new SportProfile
		{
			Sport = Sport.Hockey,
			Level = Level.College,
			Summary = "34 games in Friday/Saturday weekend series against the same opponent, October to early March, holiday break in December. Conference playoffs: best-of-3 quarterfinals on campus, then semifinals and final at a neutral arena.",
			GamesPerTeam = 34,
			Start = (int y) => GameClock.Nth(y, 10, DayOfWeek.Friday, 1),
			End = (int y) => GameClock.OnOrAfter(GameClock.D(y + 1, 3, 1), DayOfWeek.Saturday),
			Breaks = (int y) => new DateRange[1]
			{
				new DateRange(GameClock.D(y, 12, 15), GameClock.D(y, 12, 27))
			},
			Week = new SlotTemplate[1]
			{
				new SlotTemplate
				{
					Anchor = DayOfWeek.Friday,
					Length = 2,
					HomeAndHomeChance = 0.3,
					Times = (DayOfWeek d, bool last) => (d == DayOfWeek.Friday) ? new TimeOption[3]
					{
						T(1900, 5.0),
						T(1930, 2.0),
						T(1800, 1.0)
					} : new TimeOption[4]
					{
						T(1900, 4.0),
						T(1600, 1.0),
						T(1800, 1.0),
						T(1700, 1.0)
					}
				}
			},
			PlayoffShare = 1.0,
			MaxPlayoffTeams = 8,
			ChampionLabel = "Conference Champions",
			RoundsFromFinal = new PlayoffRoundFormat[4]
			{
				new PlayoffRoundFormat
				{
					Name = "Championship",
					Neutral = true,
					StartDays = Days(DayOfWeek.Saturday),
					RestDays = 0,
					Times = (DayOfWeek d) => new TimeOption[1] { T(1900, 1.0) }
				},
				new PlayoffRoundFormat
				{
					Name = "Semifinals",
					Neutral = true,
					StartDays = Days(DayOfWeek.Friday),
					RestDays = 4,
					Times = (DayOfWeek d) => Spread(1600, 1930)
				},
				new PlayoffRoundFormat
				{
					Name = "Quarterfinals",
					BestOf = 3,
					DayOffsets = new int[3] { 0, 1, 2 },
					HighSeedHome = new bool[3] { true, true, true },
					StartDays = Days(DayOfWeek.Friday),
					RestDays = 4,
					Times = (DayOfWeek d) => new TimeOption[2]
					{
						T(1900, 3.0),
						T(1600, 1.0)
					}
				},
				new PlayoffRoundFormat
				{
					Name = "First Round",
					BestOf = 3,
					DayOffsets = new int[3] { 0, 1, 2 },
					HighSeedHome = new bool[3] { true, true, true },
					StartDays = Days(DayOfWeek.Friday),
					RestDays = 4,
					Times = (DayOfWeek d) => new TimeOption[2]
					{
						T(1900, 3.0),
						T(1600, 1.0)
					}
				}
			}
		});
		list.Add(new SportProfile
		{
			Sport = Sport.Baseball,
			Level = Level.College,
			Summary = "56 games, mid-February to mid-May: Friday/Saturday/Sunday series plus midweek games. Super Regionals (best-of-3 on campus) in early June, then best-of-3 finals at a neutral site in late June.",
			GamesPerTeam = 56,
			Start = (int y) => GameClock.OnOrAfter(GameClock.D(y, 2, 13), DayOfWeek.Friday),
			End = (int y) => GameClock.OnOrAfter(GameClock.D(y, 5, 14), DayOfWeek.Saturday).AddDays(1.0),
			Week = new SlotTemplate[3]
			{
				new SlotTemplate
				{
					Anchor = DayOfWeek.Tuesday,
					Times = (DayOfWeek d, bool last) => new TimeOption[3]
					{
						T(1800, 3.0),
						T(1830, 2.0),
						T(1600, 1.0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Wednesday,
					Optional = true,
					Times = (DayOfWeek d, bool last) => new TimeOption[2]
					{
						T(1800, 3.0),
						T(1830, 2.0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Friday,
					Length = 3,
					Times = (DayOfWeek d, bool last) => d switch
					{
						DayOfWeek.Saturday => new TimeOption[4]
						{
							T(1400, 3.0),
							T(1300, 2.0),
							T(1600, 1.0),
							T(1800, 1.0)
						}, 
						DayOfWeek.Friday => new TimeOption[3]
						{
							T(1830, 3.0),
							T(1800, 2.0),
							T(1900, 1.0)
						}, 
						_ => new TimeOption[3]
						{
							T(1300, 4.0),
							T(1200, 2.0),
							T(1400, 1.0)
						}, 
					}
				}
			},
			PlayoffShare = 0.5,
			MaxPlayoffTeams = 8,
			ChampionLabel = "National Champions",
			RoundsFromFinal = new PlayoffRoundFormat[3]
			{
				new PlayoffRoundFormat
				{
					Name = "College World Series Finals",
					BestOf = 3,
					DayOffsets = new int[3] { 0, 1, 2 },
					HighSeedHome = new bool[3] { true, false, true },
					Neutral = true,
					FixedDate = (int y) => GameClock.OnOrAfter(GameClock.D(y, 6, 19), DayOfWeek.Saturday),
					RestDays = 5,
					Times = (DayOfWeek d) => (d == DayOfWeek.Sunday) ? new TimeOption[1] { T(1400, 1.0) } : new TimeOption[1] { T(1900, 1.0) }
				},
				new PlayoffRoundFormat
				{
					Name = "Super Regional",
					BestOf = 3,
					DayOffsets = new int[3] { 0, 1, 2 },
					HighSeedHome = new bool[3] { true, true, true },
					FixedDate = (int y) => GameClock.OnOrAfter(GameClock.D(y, 6, 5), DayOfWeek.Friday),
					RestDays = 4,
					Times = (DayOfWeek d) => new TimeOption[4]
					{
						T(1200, 1.0),
						T(1500, 1.0),
						T(1800, 1.0),
						T(2100, 1.0)
					}
				},
				new PlayoffRoundFormat
				{
					Name = "Regional Final",
					BestOf = 3,
					DayOffsets = new int[3] { 0, 1, 2 },
					HighSeedHome = new bool[3] { true, true, true },
					FixedDate = (int y) => GameClock.OnOrAfter(GameClock.D(y, 5, 29), DayOfWeek.Friday),
					RestDays = 4,
					Times = (DayOfWeek d) => new TimeOption[4]
					{
						T(1200, 1.0),
						T(1500, 1.0),
						T(1800, 1.0),
						T(2100, 1.0)
					}
				}
			}
		});
		list.Add(new SportProfile
		{
			Sport = Sport.Soccer,
			Level = Level.College,
			Summary = "18 matches, late August to mid-November, Thursday or Friday nights and Sunday afternoons. NCAA rounds on campus, then the College Cup at a neutral site in December (Friday semifinals, Monday final).",
			GamesPerTeam = 18,
			Start = (int y) => GameClock.OnOrAfter(GameClock.D(y, 8, 20), DayOfWeek.Thursday),
			End = (int y) => GameClock.OnOrAfter(GameClock.D(y, 11, 7), DayOfWeek.Saturday).AddDays(1.0),
			Week = new SlotTemplate[2]
			{
				new SlotTemplate
				{
					Anchor = DayOfWeek.Thursday,
					Times = (DayOfWeek d, bool last) => new TimeOption[4]
					{
						T(0, 1900, 2.0, 0),
						T(1, 1900, 3.0, 0),
						T(1, 1800, 1.0, 0),
						T(1, 1930, 1.0, 0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Sunday,
					Times = (DayOfWeek d, bool last) => new TimeOption[4]
					{
						T(0, 1300, 3.0, 0),
						T(0, 1200, 1.0, 0),
						T(0, 1400, 1.0, 0),
						T(0, 1700, 1.0, 0)
					}
				}
			},
			PlayoffShare = 0.5,
			MaxPlayoffTeams = 8,
			ChampionLabel = "National Champions",
			RoundsFromFinal = new PlayoffRoundFormat[3]
			{
				new PlayoffRoundFormat
				{
					Name = "College Cup Final",
					Neutral = true,
					StartDays = Days(DayOfWeek.Monday),
					RestDays = 2,
					Times = (DayOfWeek d) => new TimeOption[1] { T(1800, 1.0) }
				},
				new PlayoffRoundFormat
				{
					Name = "College Cup Semifinal",
					Neutral = true,
					FixedDate = (int y) => GameClock.OnOrAfter(GameClock.D(y, 12, 8), DayOfWeek.Friday),
					StartDays = Days(DayOfWeek.Friday),
					RestDays = 4,
					Times = (DayOfWeek d) => Spread(1700, 1930)
				},
				new PlayoffRoundFormat
				{
					Name = "NCAA Quarterfinal",
					StartDays = Days(DayOfWeek.Saturday, DayOfWeek.Sunday),
					RestDays = 3,
					Times = (DayOfWeek d) => new TimeOption[4]
					{
						T(1200, 1.0),
						T(1300, 1.0),
						T(1500, 1.0),
						T(1900, 1.0)
					}
				}
			}
		});
		list.Add(new SportProfile
		{
			Sport = Sport.Football,
			Level = Level.HighSchool,
			Summary = "10 games on Friday nights (one bye week), late August to the end of October. State playoffs on Fridays at the higher seed, state final at a neutral stadium in mid-December.",
			GamesPerTeam = 10,
			ByesPerTeam = 1,
			Weekly = true,
			Start = (int y) => GameClock.OnOrAfter(GameClock.D(y, 8, 20), DayOfWeek.Friday),
			End = (int y) => GameClock.OnOrAfter(GameClock.D(y, 8, 20), DayOfWeek.Friday).AddDays(70.0),
			Week = new SlotTemplate[1]
			{
				new SlotTemplate
				{
					Anchor = DayOfWeek.Friday,
					Times = (DayOfWeek d, bool last) => new TimeOption[4]
					{
						T(0, 1930, 9.0, 0),
						T(0, 1900, 2.0, 0),
						T(-1, 1930, 0.5, 1),
						T(1, 1300, 0.3, 1)
					}
				}
			},
			PlayoffShare = 0.5,
			MaxPlayoffTeams = 32,
			PlayoffsBackwardFromFinal = true,
			ChampionLabel = "State Champions",
			RoundsFromFinal = new PlayoffRoundFormat[5]
			{
				new PlayoffRoundFormat
				{
					Name = "State Championship",
					Neutral = true,
					FixedDate = (int y) => GameClock.OnOrAfter(GameClock.D(y, 12, 14), DayOfWeek.Monday),
					RestDays = 7,
					Times = (DayOfWeek d) => new TimeOption[3]
					{
						T(1200, 1.0),
						T(1530, 1.0),
						T(1900, 1.0)
					}
				},
				new PlayoffRoundFormat
				{
					Name = "Semifinals",
					StartDays = Days(DayOfWeek.Friday),
					RestDays = 6,
					Times = (DayOfWeek d) => new TimeOption[1] { T(1930, 1.0) }
				},
				new PlayoffRoundFormat
				{
					Name = "Quarterfinals",
					StartDays = Days(DayOfWeek.Friday),
					RestDays = 6,
					Times = (DayOfWeek d) => new TimeOption[1] { T(1930, 1.0) }
				},
				new PlayoffRoundFormat
				{
					Name = "Second Round",
					StartDays = Days(DayOfWeek.Friday),
					RestDays = 6,
					Times = (DayOfWeek d) => new TimeOption[1] { T(1930, 1.0) }
				},
				new PlayoffRoundFormat
				{
					Name = "First Round",
					StartDays = Days(DayOfWeek.Friday),
					RestDays = 6,
					Times = (DayOfWeek d) => new TimeOption[1] { T(1930, 1.0) }
				}
			}
		});
		list.Add(new SportProfile
		{
			Sport = Sport.Basketball,
			Level = Level.HighSchool,
			Summary = "25 games, mid-November to early February, Tuesday and Friday nights plus some Saturday tournaments. State playoffs in late February at the higher seed, neutral-site semifinals and final in March.",
			GamesPerTeam = 25,
			Start = (int y) => GameClock.OnOrAfter(GameClock.D(y, 11, 16), DayOfWeek.Tuesday),
			End = (int y) => GameClock.OnOrAfter(GameClock.D(y + 1, 2, 6), DayOfWeek.Friday),
			Breaks = (int y) => new DateRange[3]
			{
				new DateRange(GameClock.Nth(y, 11, DayOfWeek.Thursday, 4).AddDays(-1.0), GameClock.Nth(y, 11, DayOfWeek.Thursday, 4).AddDays(3.0)),
				new DateRange(GameClock.D(y, 12, 23), GameClock.D(y, 12, 26)),
				new DateRange(GameClock.D(y + 1, 1, 1), GameClock.D(y + 1, 1, 1))
			},
			Week = new SlotTemplate[3]
			{
				new SlotTemplate
				{
					Anchor = DayOfWeek.Tuesday,
					Times = (DayOfWeek d, bool last) => new TimeOption[3]
					{
						T(1930, 5.0),
						T(1800, 2.0),
						T(2000, 1.0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Friday,
					Times = (DayOfWeek d, bool last) => new TimeOption[3]
					{
						T(1930, 5.0),
						T(1800, 2.0),
						T(2000, 1.0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Saturday,
					Optional = true,
					Times = (DayOfWeek d, bool last) => new TimeOption[3]
					{
						T(1400, 2.0),
						T(1800, 2.0),
						T(1930, 1.0)
					}
				}
			},
			PlayoffShare = 0.5,
			MaxPlayoffTeams = 32,
			PlayoffsBackwardFromFinal = true,
			ChampionLabel = "State Champions",
			RoundsFromFinal = new PlayoffRoundFormat[5]
			{
				new PlayoffRoundFormat
				{
					Name = "State Championship",
					Neutral = true,
					FixedDate = (int y) => GameClock.OnOrAfter(GameClock.D(y + 1, 3, 5), DayOfWeek.Saturday),
					RestDays = 2,
					Times = (DayOfWeek d) => new TimeOption[4]
					{
						T(1200, 1.0),
						T(1400, 1.0),
						T(1800, 1.0),
						T(2000, 1.0)
					}
				},
				new PlayoffRoundFormat
				{
					Name = "Semifinals",
					Neutral = true,
					StartDays = Days(DayOfWeek.Tuesday, DayOfWeek.Wednesday),
					RestDays = 2,
					Times = (DayOfWeek d) => Spread(1800, 2000)
				},
				new PlayoffRoundFormat
				{
					Name = "Quarterfinals",
					StartDays = Days(DayOfWeek.Friday, DayOfWeek.Saturday),
					RestDays = 2,
					Times = (DayOfWeek d) => new TimeOption[1] { T(1930, 1.0) }
				},
				new PlayoffRoundFormat
				{
					Name = "Second Round",
					StartDays = Days(DayOfWeek.Tuesday, DayOfWeek.Wednesday),
					RestDays = 2,
					Times = (DayOfWeek d) => new TimeOption[1] { T(1930, 1.0) }
				},
				new PlayoffRoundFormat
				{
					Name = "First Round",
					StartDays = Days(DayOfWeek.Friday, DayOfWeek.Saturday),
					RestDays = 2,
					Times = (DayOfWeek d) => new TimeOption[1] { T(1930, 1.0) }
				}
			}
		});
		Func<DayOfWeek, TimeOption[]> hsBaseballPlayoffTimes = (DayOfWeek d) => Weekend(d) ? new TimeOption[1] { T(1200, 1.0) } : new TimeOption[1] { T(1700, 1.0) };
		list.Add(new SportProfile
		{
			Sport = Sport.Baseball,
			Level = Level.HighSchool,
			Summary = "25 games, mid-February to mid-April, weekday games at 5:30 and some Saturdays, spring break in early April. Every state playoff round is best-of-3 (a doubleheader, then game 3 if needed), finals in late May at a neutral park.",
			GamesPerTeam = 25,
			Start = (int y) => GameClock.OnOrAfter(GameClock.D(y, 2, 10), DayOfWeek.Tuesday),
			End = (int y) => GameClock.OnOrAfter(GameClock.D(y, 4, 11), DayOfWeek.Saturday),
			Breaks = (int y) => new DateRange[1]
			{
				new DateRange(GameClock.OnOrAfter(GameClock.D(y, 4, 1), DayOfWeek.Monday), GameClock.OnOrAfter(GameClock.D(y, 4, 1), DayOfWeek.Monday).AddDays(4.0))
			},
			Week = new SlotTemplate[4]
			{
				new SlotTemplate
				{
					Anchor = DayOfWeek.Tuesday,
					Times = (DayOfWeek d, bool last) => new TimeOption[4]
					{
						T(1730, 2.0),
						T(1755, 2.0),
						T(1800, 1.0),
						T(1630, 1.0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Friday,
					Times = (DayOfWeek d, bool last) => new TimeOption[3]
					{
						T(1730, 2.0),
						T(1900, 2.0),
						T(1800, 1.0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Saturday,
					Optional = true,
					Times = (DayOfWeek d, bool last) => new TimeOption[3]
					{
						T(1100, 1.0),
						T(1200, 2.0),
						T(1400, 1.0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Wednesday,
					Optional = true,
					Times = (DayOfWeek d, bool last) => new TimeOption[2]
					{
						T(1730, 2.0),
						T(1800, 1.0)
					}
				}
			},
			PlayoffShare = 0.5,
			MaxPlayoffTeams = 32,
			PlayoffsBackwardFromFinal = true,
			ChampionLabel = "State Champions",
			RoundsFromFinal = new PlayoffRoundFormat[5]
			{
				new PlayoffRoundFormat
				{
					Name = "State Championship Series",
					BestOf = 3,
					DayOffsets = new int[3] { 0, 0, 1 },
					HighSeedHome = new bool[3] { true, false, true },
					Neutral = true,
					FixedDate = (int y) => GameClock.OnOrAfter(GameClock.D(y, 5, 20), DayOfWeek.Friday),
					RestDays = 3,
					Times = (DayOfWeek d) => (d == DayOfWeek.Saturday) ? new TimeOption[1] { T(1200, 1.0) } : new TimeOption[1] { T(1100, 1.0) }
				},
				HsBaseballSeries("Semifinals", Days(DayOfWeek.Wednesday, DayOfWeek.Thursday)),
				HsBaseballSeries("Quarterfinals", Days(DayOfWeek.Wednesday, DayOfWeek.Thursday)),
				HsBaseballSeries("Second Round", Days(DayOfWeek.Wednesday, DayOfWeek.Thursday)),
				HsBaseballSeries("First Round", Days(DayOfWeek.Wednesday, DayOfWeek.Thursday))
			}
		});
		list.Add(new SportProfile
		{
			Sport = Sport.Soccer,
			Level = Level.HighSchool,
			Summary = "16 matches in the spring, late January to mid-April, Tuesday and Friday nights. State playoffs at the higher seed, final at a neutral stadium in May.",
			GamesPerTeam = 16,
			Start = (int y) => GameClock.OnOrAfter(GameClock.D(y, 1, 27), DayOfWeek.Tuesday),
			End = (int y) => GameClock.OnOrAfter(GameClock.D(y, 4, 10), DayOfWeek.Friday),
			Breaks = (int y) => new DateRange[1]
			{
				new DateRange(GameClock.OnOrAfter(GameClock.D(y, 4, 1), DayOfWeek.Monday), GameClock.OnOrAfter(GameClock.D(y, 4, 1), DayOfWeek.Monday).AddDays(4.0))
			},
			Week = new SlotTemplate[2]
			{
				new SlotTemplate
				{
					Anchor = DayOfWeek.Tuesday,
					Times = (DayOfWeek d, bool last) => new TimeOption[3]
					{
						T(1930, 3.0),
						T(1730, 2.0),
						T(1800, 1.0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Friday,
					Times = (DayOfWeek d, bool last) => new TimeOption[2]
					{
						T(1930, 3.0),
						T(1730, 2.0)
					}
				}
			},
			PlayoffShare = 0.5,
			MaxPlayoffTeams = 32,
			PlayoffsBackwardFromFinal = true,
			ChampionLabel = "State Champions",
			RoundsFromFinal = new PlayoffRoundFormat[5]
			{
				new PlayoffRoundFormat
				{
					Name = "State Championship",
					Neutral = true,
					FixedDate = (int y) => GameClock.OnOrAfter(GameClock.D(y, 5, 7), DayOfWeek.Friday),
					RestDays = 5,
					Times = (DayOfWeek d) => new TimeOption[2]
					{
						T(1730, 1.0),
						T(2000, 1.0)
					}
				},
				new PlayoffRoundFormat
				{
					Name = "Semifinals",
					StartDays = Days(DayOfWeek.Tuesday, DayOfWeek.Friday),
					RestDays = 3,
					Times = (DayOfWeek d) => new TimeOption[1] { T(1930, 1.0) }
				},
				new PlayoffRoundFormat
				{
					Name = "Quarterfinals",
					StartDays = Days(DayOfWeek.Tuesday, DayOfWeek.Friday),
					RestDays = 3,
					Times = (DayOfWeek d) => new TimeOption[1] { T(1930, 1.0) }
				},
				new PlayoffRoundFormat
				{
					Name = "Second Round",
					StartDays = Days(DayOfWeek.Tuesday, DayOfWeek.Friday),
					RestDays = 3,
					Times = (DayOfWeek d) => new TimeOption[1] { T(1930, 1.0) }
				},
				new PlayoffRoundFormat
				{
					Name = "First Round",
					StartDays = Days(DayOfWeek.Tuesday, DayOfWeek.Friday),
					RestDays = 3,
					Times = (DayOfWeek d) => new TimeOption[1] { T(1930, 1.0) }
				}
			}
		});
		list.Add(new SportProfile
		{
			Sport = Sport.Hockey,
			Level = Level.HighSchool,
			Summary = "25 games, late November to mid-February, Tuesday and Thursday nights and Saturday afternoons. Section playoffs in late February at the higher seed, then the state tournament at a big arena in March.",
			GamesPerTeam = 25,
			Start = (int y) => GameClock.OnOrAfter(GameClock.D(y, 11, 24), DayOfWeek.Tuesday),
			End = (int y) => GameClock.OnOrAfter(GameClock.D(y + 1, 2, 12), DayOfWeek.Saturday),
			Breaks = (int y) => new DateRange[2]
			{
				new DateRange(GameClock.D(y, 12, 24), GameClock.D(y, 12, 25)),
				new DateRange(GameClock.D(y, 12, 31), GameClock.D(y + 1, 1, 1))
			},
			Week = new SlotTemplate[3]
			{
				new SlotTemplate
				{
					Anchor = DayOfWeek.Tuesday,
					Times = (DayOfWeek d, bool last) => new TimeOption[2]
					{
						T(1900, 3.0),
						T(1915, 1.0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Thursday,
					Optional = true,
					Times = (DayOfWeek d, bool last) => new TimeOption[2]
					{
						T(1900, 3.0),
						T(1915, 1.0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Saturday,
					Times = (DayOfWeek d, bool last) => new TimeOption[3]
					{
						T(1400, 1.0),
						T(1500, 1.0),
						T(1900, 2.0)
					}
				}
			},
			PlayoffShare = 0.5,
			MaxPlayoffTeams = 16,
			PlayoffsBackwardFromFinal = true,
			ChampionLabel = "State Champions",
			RoundsFromFinal = new PlayoffRoundFormat[5]
			{
				new PlayoffRoundFormat
				{
					Name = "State Championship",
					Neutral = true,
					FixedDate = (int y) => GameClock.OnOrAfter(GameClock.D(y + 1, 3, 6), DayOfWeek.Saturday),
					RestDays = 0,
					Times = (DayOfWeek d) => new TimeOption[1] { T(2000, 1.0) }
				},
				new PlayoffRoundFormat
				{
					Name = "State Semifinals",
					Neutral = true,
					StartDays = Days(DayOfWeek.Friday),
					RestDays = 0,
					Times = (DayOfWeek d) => Spread(1100, 1300, 1800, 2000)
				},
				new PlayoffRoundFormat
				{
					Name = "State Quarterfinals",
					Neutral = true,
					StartDays = Days(DayOfWeek.Wednesday, DayOfWeek.Thursday),
					RestDays = 4,
					Times = (DayOfWeek d) => Spread(1100, 1300, 1800, 2000)
				},
				new PlayoffRoundFormat
				{
					Name = "Section Final",
					StartDays = Days(DayOfWeek.Thursday, DayOfWeek.Friday),
					RestDays = 2,
					Times = (DayOfWeek d) => new TimeOption[1] { T(1900, 1.0) }
				},
				new PlayoffRoundFormat
				{
					Name = "Section Semifinal",
					StartDays = Days(DayOfWeek.Tuesday, DayOfWeek.Wednesday),
					RestDays = 2,
					Times = (DayOfWeek d) => new TimeOption[1] { T(1900, 1.0) }
				}
			}
		});
		list.Add(new SportProfile
		{
			Sport = Sport.Hockey,
			Level = Level.Junior,
			Summary = "62 games, late September to early April, mostly Friday and Saturday nights at 7:05 with a few Sunday afternoons and midweek games. Most of the league makes the playoffs: a best-of-3 first round (the top seeds get a bye), then best-of-5 series to the Junior Cup in May.",
			GamesPerTeam = 62,
			Start = (int y) => GameClock.Last(y, 9, DayOfWeek.Friday),
			End = (int y) => GameClock.Nth(y + 1, 4, DayOfWeek.Saturday, 1),
			Breaks = (int y) => new DateRange[1]
			{
				new DateRange(GameClock.D(y, 12, 21), GameClock.D(y, 12, 28))
			},
			Week = new SlotTemplate[3]
			{
				new SlotTemplate
				{
					Anchor = DayOfWeek.Friday,
					Length = 2,
					HomeAndHomeChance = 0.35,
					Times = (DayOfWeek d, bool last) => (d == DayOfWeek.Friday) ? new TimeOption[3]
					{
						T(1905, 5.0),
						T(1835, 1.0),
						T(1930, 1.0)
					} : new TimeOption[3]
					{
						T(1905, 5.0),
						T(1805, 1.0),
						T(1600, 1.0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Sunday,
					Optional = true,
					Times = (DayOfWeek d, bool last) => new TimeOption[2]
					{
						T(1505, 2.0),
						T(1705, 1.0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Wednesday,
					Optional = true,
					Times = (DayOfWeek d, bool last) => new TimeOption[2]
					{
						T(1905, 2.0),
						T(1835, 1.0)
					}
				}
			},
			PlayoffShare = 0.75,
			PlayoffPowerOfTwo = false,
			MaxPlayoffTeams = 12,
			ChampionLabel = "Junior Cup Champions",
			RoundsFromFinal = new PlayoffRoundFormat[4]
			{
				JuniorSeries("Junior Cup Final", 3),
				JuniorSeries("Conference Final", 2),
				JuniorSeries("Conference Semifinal", 2),
				new PlayoffRoundFormat
				{
					Name = "First Round",
					BestOf = 3,
					DayOffsets = new int[3] { 0, 1, 2 },
					HighSeedHome = new bool[3] { true, true, true },
					StartDays = Days(DayOfWeek.Friday),
					RestDays = 5,
					Times = (DayOfWeek d) => new TimeOption[1] { T(1905, 1.0) }
				}
			}
		});
		Func<DayOfWeek, TimeOption[]> minorHockeyPlayoffTimes = (DayOfWeek d) => Weekend(d) ? new TimeOption[3]
		{
			T(1500, 1.0),
			T(1700, 2.0),
			T(1900, 3.0)
		} : new TimeOption[2]
		{
			T(1900, 4.0),
			T(1905, 2.0)
		};
		list.Add(new SportProfile
		{
			Sport = Sport.Hockey,
			Level = Level.Minor,
			Summary = "72 games, mid-October to mid-April, mostly Friday, Saturday and Sunday with a few midweek and morning school-day games. Most of the league makes the playoffs: a best-of-3 first round (top seeds get a bye), best-of-5 division rounds, then best-of-7 to the Cup in June.",
			GamesPerTeam = 72,
			Start = (int y) => GameClock.OnOrAfter(GameClock.D(y, 10, 9), DayOfWeek.Friday),
			End = (int y) => GameClock.OnOrAfter(GameClock.D(y + 1, 4, 13), DayOfWeek.Sunday),
			Breaks = (int y) => new DateRange[2]
			{
				new DateRange(GameClock.D(y, 12, 23), GameClock.D(y, 12, 26)),
				new DateRange(GameClock.Nth(y + 1, 2, DayOfWeek.Sunday, 1).AddDays(-1.0), GameClock.Nth(y + 1, 2, DayOfWeek.Sunday, 1).AddDays(2.0))
			},
			Week = new SlotTemplate[4]
			{
				new SlotTemplate
				{
					Anchor = DayOfWeek.Friday,
					Times = (DayOfWeek d, bool last) => new TimeOption[3]
					{
						T(1900, 5.0),
						T(1905, 2.0),
						T(1930, 1.0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Saturday,
					Times = (DayOfWeek d, bool last) => new TimeOption[4]
					{
						T(1900, 4.0),
						T(1800, 2.0),
						T(1700, 1.0),
						T(1905, 1.0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Sunday,
					Optional = true,
					Times = (DayOfWeek d, bool last) => new TimeOption[3]
					{
						T(1500, 3.0),
						T(1700, 2.0),
						T(1400, 1.0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Wednesday,
					Optional = true,
					Times = (DayOfWeek d, bool last) => new TimeOption[3]
					{
						T(1900, 4.0),
						T(1030, 1.0),
						T(1100, 1.0)
					}
				}
			},
			PlayoffShare = 0.72,
			PlayoffPowerOfTwo = false,
			MaxPlayoffTeams = 16,
			ChampionLabel = "Cup Champions",
			RoundsFromFinal = new PlayoffRoundFormat[5]
			{
				Series7("Cup Finals", minorHockeyPlayoffTimes, 2),
				Series7("Conference Finals", minorHockeyPlayoffTimes, 2),
				MinorSeries("Division Finals", minorHockeyPlayoffTimes, 2),
				MinorSeries("Division Semifinals", minorHockeyPlayoffTimes, 2),
				new PlayoffRoundFormat
				{
					Name = "First Round",
					BestOf = 3,
					DayOffsets = new int[3] { 0, 1, 2 },
					HighSeedHome = new bool[3] { false, true, true },
					RestDays = 4,
					Times = minorHockeyPlayoffTimes
				}
			}
		});
		list.Add(new SportProfile
		{
			Sport = Sport.Basketball,
			Level = Level.Minor,
			Summary = "50 games, early November to late March (with a break for the Winter Showcase around Christmas), mostly 7 PM tips plus some 11 AM school-day games. The top half makes the playoffs: single games through the conference finals, then a best-of-3 Finals in April.",
			GamesPerTeam = 50,
			Start = (int y) => GameClock.OnOrAfter(GameClock.D(y, 11, 6), DayOfWeek.Friday),
			End = (int y) => GameClock.Last(y + 1, 3, DayOfWeek.Saturday),
			Breaks = (int y) => new DateRange[3]
			{
				new DateRange(GameClock.Nth(y, 11, DayOfWeek.Thursday, 4), GameClock.Nth(y, 11, DayOfWeek.Thursday, 4)),
				new DateRange(GameClock.D(y, 12, 19), GameClock.D(y, 12, 26)),
				new DateRange(GameClock.Nth(y + 1, 2, DayOfWeek.Sunday, 3).AddDays(-2.0), GameClock.Nth(y + 1, 2, DayOfWeek.Sunday, 3).AddDays(1.0))
			},
			Week = new SlotTemplate[3]
			{
				new SlotTemplate
				{
					Anchor = DayOfWeek.Tuesday,
					Times = (DayOfWeek d, bool last) => new TimeOption[4]
					{
						T(0, 1900, 4.0, 0),
						T(0, 1100, 1.0, 0),
						T(1, 1900, 3.0, 0),
						T(1, 1930, 1.0, 0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Friday,
					Times = (DayOfWeek d, bool last) => new TimeOption[4]
					{
						T(0, 1900, 4.0, 0),
						T(0, 1930, 1.0, 0),
						T(1, 1900, 3.0, 0),
						T(1, 1700, 1.0, 0)
					}
				},
				new SlotTemplate
				{
					Anchor = DayOfWeek.Sunday,
					Optional = true,
					Times = (DayOfWeek d, bool last) => new TimeOption[3]
					{
						T(1500, 2.0),
						T(1700, 1.0),
						T(1300, 1.0)
					}
				}
			},
			PlayoffShare = 0.5,
			MaxPlayoffTeams = 8,
			ChampionLabel = "Champions",
			RoundsFromFinal = new PlayoffRoundFormat[3]
			{
				new PlayoffRoundFormat
				{
					Name = "Finals",
					BestOf = 3,
					DayOffsets = new int[3] { 0, 2, 4 },
					HighSeedHome = new bool[3] { false, true, true },
					RestDays = 3,
					Times = (DayOfWeek d) => new TimeOption[2]
					{
						T(1900, 2.0),
						T(1930, 1.0)
					}
				},
				new PlayoffRoundFormat
				{
					Name = "Conference Finals",
					RestDays = 2,
					Times = (DayOfWeek d) => new TimeOption[2]
					{
						T(1900, 2.0),
						T(1930, 1.0)
					}
				},
				new PlayoffRoundFormat
				{
					Name = "Conference Semifinals",
					RestDays = 2,
					Times = (DayOfWeek d) => new TimeOption[2]
					{
						T(1900, 2.0),
						T(1930, 1.0)
					}
				}
			}
		});
		Func<DayOfWeek, bool, TimeOption[]> minorBaseballTimes = (DayOfWeek d, bool last) => d switch
		{
			DayOfWeek.Sunday => new TimeOption[3]
			{
				T(1305, 3.0),
				T(1335, 1.0),
				T(1405, 1.0)
			}, 
			DayOfWeek.Saturday => new TimeOption[4]
			{
				T(1805, 2.0),
				T(1835, 1.0),
				T(1905, 2.0),
				T(1605, 1.0)
			}, 
			DayOfWeek.Wednesday => new TimeOption[4]
			{
				T(1835, 2.0),
				T(1905, 3.0),
				T(1105, 1.0),
				T(1205, 1.0)
			}, 
			_ => new TimeOption[3]
			{
				T(1835, 2.0),
				T(1905, 4.0),
				T(1835, 1.0)
			}, 
		};
		list.Add(new SportProfile
		{
			Sport = Sport.Baseball,
			Level = Level.Minor,
			Summary = "150 games, late March to late September, six-game series Tuesday to Sunday with Mondays off (7:05 PM nights, Sunday afternoons, some school-day games at 11:05 AM). The top 4 play best-of-3 semifinals, then one championship game at a neutral ballpark.",
			GamesPerTeam = 150,
			Start = (int y) => GameClock.OnOrAfter(GameClock.D(y, 3, 24), DayOfWeek.Tuesday),
			End = (int y) => GameClock.OnOrAfter(GameClock.D(y, 9, 14), DayOfWeek.Sunday),
			Week = new SlotTemplate[1]
			{
				new SlotTemplate
				{
					Anchor = DayOfWeek.Tuesday,
					Length = 6,
					Times = minorBaseballTimes
				}
			},
			PlayoffShare = 0.4,
			PlayoffPowerOfTwo = true,
			MinPlayoffTeams = 2,
			MaxPlayoffTeams = 4,
			ChampionLabel = "Champions",
			RoundsFromFinal = new PlayoffRoundFormat[2]
			{
				new PlayoffRoundFormat
				{
					Name = "Championship Game",
					Neutral = true,
					RestDays = 2,
					Times = (DayOfWeek d) => new TimeOption[1] { T(1905, 1.0) }
				},
				new PlayoffRoundFormat
				{
					Name = "Semifinals",
					BestOf = 3,
					DayOffsets = new int[3] { 0, 1, 2 },
					HighSeedHome = new bool[3] { true, true, true },
					RestDays = 1,
					Times = (DayOfWeek d) => Weekend(d) ? new TimeOption[2]
					{
						T(1305, 1.0),
						T(1805, 1.0)
					} : new TimeOption[2]
					{
						T(1905, 2.0),
						T(1835, 1.0)
					}
				}
			}
		});
		Dictionary<string, SportProfile> map = new Dictionary<string, SportProfile>();
		foreach (SportProfile p in list)
		{
			map[p.Sport.ToString() + "-" + p.Level] = p;
		}
		Sport[] allSports = SportRules.AllSports;
		for (int num = 0; num < allSports.Length; num++)
		{
			Sport s = allSports[num];
			Level[] allLevels = SportRules.AllLevels;
			for (int num2 = 0; num2 < allLevels.Length; num2++)
			{
				Level l = allLevels[num2];
				if (!map.ContainsKey(s.ToString() + "-" + l))
				{
					map[s.ToString() + "-" + l] = map[s.ToString() + "-" + Level.Pro];
				}
			}
		}
		return map;
		PlayoffRoundFormat HsBaseballSeries(string name, DayOfWeek[] days)
		{
			return new PlayoffRoundFormat
			{
				Name = name,
				BestOf = 3,
				DayOffsets = new int[3] { 0, 0, 1 },
				HighSeedHome = new bool[3] { true, false, true },
				StartDays = days,
				RestDays = 2,
				Times = hsBaseballPlayoffTimes
			};
		}
		static PlayoffRoundFormat JuniorSeries(string name, int rest)
		{
			return new PlayoffRoundFormat
			{
				Name = name,
				BestOf = 5,
				DayOffsets = new int[5] { 0, 1, 4, 5, 7 },
				HighSeedHome = new bool[5] { true, true, false, false, true },
				StartDays = Days(DayOfWeek.Friday, DayOfWeek.Saturday),
				RestDays = rest,
				Times = (DayOfWeek d) => new TimeOption[2]
				{
					T(1905, 3.0),
					T(1835, 1.0)
				}
			};
		}
		static PlayoffRoundFormat MinorSeries(string name, Func<DayOfWeek, TimeOption[]> times, int rest)
		{
			return new PlayoffRoundFormat
			{
				Name = name,
				BestOf = 5,
				DayOffsets = new int[5] { 0, 1, 3, 4, 6 },
				HighSeedHome = new bool[5] { true, true, false, false, true },
				RestDays = rest,
				Times = times,
				Stagger = 1
			};
		}
	}
}
