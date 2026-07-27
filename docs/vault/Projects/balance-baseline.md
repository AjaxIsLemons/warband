# Balance baseline — committed golden numbers

**Regenerate with `make baseline`. The A/B is `git diff`.**

Every authoring instrument (`--enc`, `--boss`, the outlier sweep, run EV, sim
health) reduced to one metric per line. This file is not an assertion and nothing
fails when it moves — it exists so a session can SEE what a change did to the game
instead of reconstructing a before from a worktree. Regenerate it as part of any
change to content, the sim, or the probes, and read the diff before committing.

`win` is the best result across the six formations; `spread` is best − worst, which
is the number that says whether placement mattered. Encounter and boss rows are per
answer axis (balanced / reach / control / damage). Party size follows the act:
act 1 = 3 heroes, act 2 = 4 heroes, act 3 = 4 heroes —
the strongest difficulty dial in the game, so every number here is conditional on it.

Byte-stable: the sim is deterministic and every probe is seeded, so an unchanged
game must reproduce this file exactly.

```
content.fingerprint                                                   3dba11673c26e858
enc.gnawing-hour.debut                                                1
enc.gnawing-hour.a1.balanced                                          win=100 spread=0 rule=100 ticks=74
enc.gnawing-hour.a1.reach                                             win=100 spread=0 rule=100 ticks=66
enc.gnawing-hour.a1.control                                           win=100 spread=0 rule=100 ticks=87
enc.gnawing-hour.a1.damage                                            win=100 spread=0 rule=100 ticks=49
enc.gnawing-hour.a2.balanced                                          win=100 spread=0 rule=100 ticks=80
enc.gnawing-hour.a2.reach                                             win=100 spread=0 rule=100 ticks=65
enc.gnawing-hour.a2.control                                           win=100 spread=0 rule=100 ticks=163
enc.gnawing-hour.a2.damage                                            win=100 spread=0 rule=100 ticks=64
enc.gnawing-hour.a3.balanced                                          win=100 spread=0 rule=100 ticks=86
enc.gnawing-hour.a3.reach                                             win=100 spread=0 rule=100 ticks=92
enc.gnawing-hour.a3.control                                           win=100 spread=0 rule=100 ticks=186
enc.gnawing-hour.a3.damage                                            win=100 spread=0 rule=100 ticks=76
enc.the-long-range.debut                                              2
enc.the-long-range.a1.balanced                                        win=0 spread=0 rule=0 ticks=125
enc.the-long-range.a1.reach                                           win=100 spread=100 rule=100 ticks=126
enc.the-long-range.a1.control                                         win=0 spread=0 rule=0 ticks=143
enc.the-long-range.a1.damage                                          win=100 spread=0 rule=100 ticks=82
enc.the-long-range.a2.balanced                                        win=100 spread=0 rule=100 ticks=114
enc.the-long-range.a2.reach                                           win=100 spread=0 rule=100 ticks=80
enc.the-long-range.a2.control                                         win=100 spread=0 rule=0 ticks=281
enc.the-long-range.a2.damage                                          win=100 spread=0 rule=100 ticks=61
enc.the-long-range.a3.balanced                                        win=100 spread=0 rule=83 ticks=116
enc.the-long-range.a3.reach                                           win=100 spread=0 rule=100 ticks=73
enc.the-long-range.a3.control                                         win=100 spread=0 rule=0 ticks=314
enc.the-long-range.a3.damage                                          win=100 spread=0 rule=100 ticks=60
enc.ninth-bell.debut                                                  1
enc.ninth-bell.a1.balanced                                            win=100 spread=0 rule=100 ticks=66
enc.ninth-bell.a1.reach                                               win=100 spread=100 rule=100 ticks=44
enc.ninth-bell.a1.control                                             win=100 spread=0 rule=100 ticks=88
enc.ninth-bell.a1.damage                                              win=100 spread=100 rule=100 ticks=53
enc.ninth-bell.a2.balanced                                            win=100 spread=0 rule=100 ticks=103
enc.ninth-bell.a2.reach                                               win=100 spread=0 rule=100 ticks=72
enc.ninth-bell.a2.control                                             win=100 spread=0 rule=83 ticks=196
enc.ninth-bell.a2.damage                                              win=100 spread=0 rule=100 ticks=72
enc.ninth-bell.a3.balanced                                            win=100 spread=0 rule=100 ticks=110
enc.ninth-bell.a3.reach                                               win=100 spread=0 rule=88 ticks=85
enc.ninth-bell.a3.control                                             win=100 spread=0 rule=100 ticks=220
enc.ninth-bell.a3.damage                                              win=100 spread=0 rule=100 ticks=73
enc.the-drop.debut                                                    1
enc.the-drop.a1.balanced                                              win=100 spread=100 rule=100 ticks=119
enc.the-drop.a1.reach                                                 win=100 spread=100 rule=100 ticks=70
enc.the-drop.a1.control                                               win=100 spread=0 rule=100 ticks=148
enc.the-drop.a1.damage                                                win=100 spread=0 rule=100 ticks=67
enc.the-drop.a2.balanced                                              win=100 spread=100 rule=100 ticks=153
enc.the-drop.a2.reach                                                 win=100 spread=0 rule=100 ticks=78
enc.the-drop.a2.control                                               win=100 spread=0 rule=100 ticks=126
enc.the-drop.a2.damage                                                win=100 spread=0 rule=100 ticks=66
enc.the-drop.a3.balanced                                              win=100 spread=100 rule=100 ticks=129
enc.the-drop.a3.reach                                                 win=100 spread=0 rule=100 ticks=76
enc.the-drop.a3.control                                               win=100 spread=0 rule=100 ticks=130
enc.the-drop.a3.damage                                                win=100 spread=0 rule=100 ticks=66
enc.slagworks.debut                                                   3
enc.slagworks.a1.balanced                                             win=0 spread=0 rule=100 ticks=103
enc.slagworks.a1.reach                                                win=100 spread=100 rule=100 ticks=111
enc.slagworks.a1.control                                              win=0 spread=0 rule=100 ticks=110
enc.slagworks.a1.damage                                               win=100 spread=100 rule=100 ticks=104
enc.slagworks.a2.balanced                                             win=100 spread=0 rule=100 ticks=118
enc.slagworks.a2.reach                                                win=100 spread=0 rule=100 ticks=105
enc.slagworks.a2.control                                              win=100 spread=0 rule=100 ticks=239
enc.slagworks.a2.damage                                               win=100 spread=0 rule=100 ticks=90
enc.slagworks.a3.balanced                                             win=100 spread=0 rule=100 ticks=120
enc.slagworks.a3.reach                                                win=100 spread=0 rule=100 ticks=104
enc.slagworks.a3.control                                              win=100 spread=0 rule=100 ticks=247
enc.slagworks.a3.damage                                               win=100 spread=0 rule=100 ticks=87
enc.long-procession.debut                                             3
enc.long-procession.a1.balanced                                       win=100 spread=0 rule=100 ticks=103
enc.long-procession.a1.reach                                          win=100 spread=100 rule=100 ticks=78
enc.long-procession.a1.control                                        win=100 spread=0 rule=100 ticks=128
enc.long-procession.a1.damage                                         win=100 spread=100 rule=100 ticks=66
enc.long-procession.a2.balanced                                       win=100 spread=0 rule=100 ticks=81
enc.long-procession.a2.reach                                          win=100 spread=0 rule=100 ticks=54
enc.long-procession.a2.control                                        win=100 spread=0 rule=67 ticks=139
enc.long-procession.a2.damage                                         win=100 spread=0 rule=85 ticks=57
enc.long-procession.a3.balanced                                       win=100 spread=0 rule=100 ticks=95
enc.long-procession.a3.reach                                          win=100 spread=0 rule=100 ticks=71
enc.long-procession.a3.control                                        win=100 spread=0 rule=83 ticks=190
enc.long-procession.a3.damage                                         win=100 spread=0 rule=85 ticks=75
enc.naive.completed                                                   2/12
enc.naive.died.act-1-node-0                                           4
enc.naive.died.act-2-node-0                                           4
enc.naive.died.act-1-node-1                                           1
enc.naive.died.act-2-node-1                                           1
boss.a1.axes-passing                                                  balanced+control+damage
boss.a1.balanced                                                      win=100 spread=100 rule=100 ticks=123
boss.a1.reach                                                         win=0 spread=0 rule=83 ticks=81
boss.a1.control                                                       win=100 spread=100 rule=83 ticks=163
boss.a1.damage                                                        win=100 spread=100 rule=100 ticks=116
boss.a2.axes-passing                                                  balanced+reach+control+damage
boss.a2.balanced                                                      win=100 spread=100 rule=100 ticks=179
boss.a2.reach                                                         win=100 spread=100 rule=100 ticks=159
boss.a2.control                                                       win=100 spread=100 rule=100 ticks=388
boss.a2.damage                                                        win=100 spread=100 rule=100 ticks=143
boss.a3.axes-passing                                                  balanced+control+damage
boss.a3.balanced                                                      win=100 spread=100 rule=83 ticks=295
boss.a3.reach                                                         win=33 spread=33 rule=100 ticks=116
boss.a3.control                                                       win=100 spread=100 rule=100 ticks=280
boss.a3.damage                                                        win=100 spread=0 rule=100 ticks=156
build.caphits                                                         0
build.mirror-nondraws                                                 57
build.class.cleric                                                    avg=47 best=69 worst=28
build.class.bulwark                                                   avg=59 best=84 worst=27
build.class.shade                                                     avg=57 best=89 worst=23
build.class.sharpshot                                                 avg=56 best=71 worst=22
build.class.pyromancer                                                avg=40 best=54 worst=34
build.class.berserker                                                 avg=75 best=81 worst=66
build.class.phalanx                                                   avg=54 best=76 worst=34
build.class.banneret                                                  avg=13 best=18 worst=7
build.node.shade.reaper-vs-shade.phantom                              delta=-52
build.node.sharpshot.sniper.onebreath-vs-sharpshot.sniper.killwindow  delta=-47
build.node.bulwark.juggernaut-vs-bulwark.warden                       delta=-46
build.node.phalanx.pikewall-vs-phalanx.lancer                         delta=30
build.node.sharpshot.sniper-vs-sharpshot.volleyer                     delta=-21
build.node.cleric.warpriest.conflagration-vs-cleric.warpriest.zeal    delta=-20
build.node.cleric.warpriest-vs-cleric.lifebinder                      delta=-19
run.stable                                                            victory=4 fightwin=76 boss=0.44 gold=17 caps=0
run.fraying                                                           victory=4 fightwin=76 boss=0.52 gold=20 caps=0
run.collapsing                                                        victory=7 fightwin=76 boss=0.55 gold=25 caps=0
build.flags                                                           6
build.flag                                                            CHASSIS-DEAD banneret (best build 18%)
build.flag                                                            DEAD banneret:warcaller+dreadpresence+bearer (7%)
build.flag                                                            NODE-LOPSIDED bulwark.juggernaut vs bulwark.warden (Δ-46)
build.flag                                                            NODE-LOPSIDED phalanx.pikewall vs phalanx.lancer (Δ30)
build.flag                                                            NODE-LOPSIDED shade.reaper vs shade.phantom (Δ-52)
build.flag                                                            NODE-LOPSIDED sharpshot.sniper.onebreath vs sharpshot.sniper.killwindow (Δ-47)
health.deadtime-pct                                                   1.81
health.never-swung-pct                                                0.00
health.frozen-pct                                                     5.17
```
