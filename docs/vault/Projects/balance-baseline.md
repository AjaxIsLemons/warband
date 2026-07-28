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
content.fingerprint                                                      28b51d860a59bba0
enc.gnawing-hour.debut                                                   1
enc.gnawing-hour.a1.balanced                                             win=100 spread=0 rule=100 ticks=74
enc.gnawing-hour.a1.reach                                                win=100 spread=0 rule=100 ticks=53
enc.gnawing-hour.a1.control                                              win=100 spread=0 rule=100 ticks=92
enc.gnawing-hour.a1.damage                                               win=100 spread=0 rule=100 ticks=50
enc.gnawing-hour.a2.balanced                                             win=100 spread=0 rule=100 ticks=81
enc.gnawing-hour.a2.reach                                                win=100 spread=0 rule=100 ticks=77
enc.gnawing-hour.a2.control                                              win=100 spread=0 rule=100 ticks=170
enc.gnawing-hour.a2.damage                                               win=100 spread=0 rule=100 ticks=61
enc.gnawing-hour.a3.balanced                                             win=100 spread=0 rule=100 ticks=103
enc.gnawing-hour.a3.reach                                                win=100 spread=0 rule=100 ticks=91
enc.gnawing-hour.a3.control                                              win=100 spread=0 rule=100 ticks=172
enc.gnawing-hour.a3.damage                                               win=100 spread=0 rule=100 ticks=73
enc.the-long-range.debut                                                 2
enc.the-long-range.a1.balanced                                           win=100 spread=100 rule=17 ticks=119
enc.the-long-range.a1.reach                                              win=100 spread=0 rule=100 ticks=113
enc.the-long-range.a1.control                                            win=0 spread=0 rule=0 ticks=147
enc.the-long-range.a1.damage                                             win=100 spread=0 rule=100 ticks=85
enc.the-long-range.a2.balanced                                           win=100 spread=0 rule=100 ticks=108
enc.the-long-range.a2.reach                                              win=100 spread=0 rule=100 ticks=78
enc.the-long-range.a2.control                                            win=100 spread=0 rule=17 ticks=284
enc.the-long-range.a2.damage                                             win=100 spread=0 rule=100 ticks=64
enc.the-long-range.a3.balanced                                           win=100 spread=0 rule=100 ticks=112
enc.the-long-range.a3.reach                                              win=100 spread=0 rule=100 ticks=74
enc.the-long-range.a3.control                                            win=100 spread=0 rule=17 ticks=307
enc.the-long-range.a3.damage                                             win=100 spread=0 rule=100 ticks=62
enc.ninth-bell.debut                                                     1
enc.ninth-bell.a1.balanced                                               win=100 spread=0 rule=100 ticks=66
enc.ninth-bell.a1.reach                                                  win=100 spread=100 rule=100 ticks=49
enc.ninth-bell.a1.control                                                win=100 spread=0 rule=100 ticks=100
enc.ninth-bell.a1.damage                                                 win=100 spread=100 rule=100 ticks=55
enc.ninth-bell.a2.balanced                                               win=100 spread=0 rule=100 ticks=105
enc.ninth-bell.a2.reach                                                  win=100 spread=0 rule=70 ticks=73
enc.ninth-bell.a2.control                                                win=100 spread=100 rule=100 ticks=187
enc.ninth-bell.a2.damage                                                 win=100 spread=42 rule=100 ticks=80
enc.ninth-bell.a3.balanced                                               win=100 spread=0 rule=100 ticks=109
enc.ninth-bell.a3.reach                                                  win=100 spread=0 rule=90 ticks=89
enc.ninth-bell.a3.control                                                win=100 spread=100 rule=100 ticks=216
enc.ninth-bell.a3.damage                                                 win=100 spread=0 rule=100 ticks=81
enc.the-drop.debut                                                       1
enc.the-drop.a1.balanced                                                 win=100 spread=100 rule=100 ticks=112
enc.the-drop.a1.reach                                                    win=100 spread=100 rule=100 ticks=67
enc.the-drop.a1.control                                                  win=100 spread=100 rule=100 ticks=159
enc.the-drop.a1.damage                                                   win=100 spread=0 rule=100 ticks=72
enc.the-drop.a2.balanced                                                 win=100 spread=100 rule=100 ticks=170
enc.the-drop.a2.reach                                                    win=100 spread=0 rule=100 ticks=83
enc.the-drop.a2.control                                                  win=100 spread=0 rule=100 ticks=150
enc.the-drop.a2.damage                                                   win=100 spread=0 rule=100 ticks=77
enc.the-drop.a3.balanced                                                 win=100 spread=100 rule=100 ticks=131
enc.the-drop.a3.reach                                                    win=100 spread=0 rule=100 ticks=86
enc.the-drop.a3.control                                                  win=100 spread=0 rule=100 ticks=154
enc.the-drop.a3.damage                                                   win=100 spread=0 rule=100 ticks=78
enc.slagworks.debut                                                      3
enc.slagworks.a1.balanced                                                win=0 spread=0 rule=100 ticks=115
enc.slagworks.a1.reach                                                   win=46 spread=46 rule=100 ticks=138
enc.slagworks.a1.control                                                 win=0 spread=0 rule=100 ticks=115
enc.slagworks.a1.damage                                                  win=100 spread=100 rule=100 ticks=114
enc.slagworks.a2.balanced                                                win=100 spread=0 rule=100 ticks=126
enc.slagworks.a2.reach                                                   win=100 spread=0 rule=100 ticks=107
enc.slagworks.a2.control                                                 win=100 spread=0 rule=100 ticks=258
enc.slagworks.a2.damage                                                  win=100 spread=0 rule=100 ticks=90
enc.slagworks.a3.balanced                                                win=100 spread=0 rule=100 ticks=124
enc.slagworks.a3.reach                                                   win=100 spread=0 rule=100 ticks=108
enc.slagworks.a3.control                                                 win=100 spread=100 rule=100 ticks=271
enc.slagworks.a3.damage                                                  win=100 spread=0 rule=100 ticks=87
enc.long-procession.debut                                                3
enc.long-procession.a1.balanced                                          win=100 spread=0 rule=100 ticks=112
enc.long-procession.a1.reach                                             win=100 spread=0 rule=100 ticks=77
enc.long-procession.a1.control                                           win=100 spread=0 rule=100 ticks=128
enc.long-procession.a1.damage                                            win=100 spread=0 rule=100 ticks=70
enc.long-procession.a2.balanced                                          win=100 spread=0 rule=100 ticks=84
enc.long-procession.a2.reach                                             win=100 spread=0 rule=83 ticks=57
enc.long-procession.a2.control                                           win=100 spread=0 rule=67 ticks=144
enc.long-procession.a2.damage                                            win=100 spread=0 rule=100 ticks=62
enc.long-procession.a3.balanced                                          win=100 spread=0 rule=100 ticks=102
enc.long-procession.a3.reach                                             win=100 spread=0 rule=100 ticks=76
enc.long-procession.a3.control                                           win=100 spread=0 rule=100 ticks=192
enc.long-procession.a3.damage                                            win=100 spread=0 rule=100 ticks=78
enc.naive.completed                                                      1/12
enc.naive.died.act-1-node-0                                              5
enc.naive.died.act-2-node-0                                              3
enc.naive.died.act-1-node-1                                              1
enc.naive.died.act-2-node-4-boss                                         1
enc.naive.died.act-3-node-4-boss                                         1
boss.a1.axes-passing                                                     balanced+damage
boss.a1.balanced                                                         win=100 spread=100 rule=83 ticks=124
boss.a1.reach                                                            win=0 spread=0 rule=83 ticks=81
boss.a1.control                                                          win=0 spread=0 rule=100 ticks=165
boss.a1.damage                                                           win=100 spread=100 rule=100 ticks=131
boss.a2.axes-passing                                                     balanced+reach+control+damage
boss.a2.balanced                                                         win=100 spread=100 rule=100 ticks=187
boss.a2.reach                                                            win=100 spread=100 rule=100 ticks=140
boss.a2.control                                                          win=100 spread=100 rule=100 ticks=353
boss.a2.damage                                                           win=100 spread=100 rule=100 ticks=140
boss.a3.axes-passing                                                     balanced+control+damage
boss.a3.balanced                                                         win=100 spread=100 rule=100 ticks=305
boss.a3.reach                                                            win=0 spread=0 rule=100 ticks=151
boss.a3.control                                                          win=100 spread=100 rule=83 ticks=265
boss.a3.damage                                                           win=100 spread=29 rule=100 ticks=166
build.caphits                                                            0
build.mirror-nondraws                                                    58
build.class.cleric                                                       avg=46 best=72 worst=27
build.class.bulwark                                                      avg=60 best=84 worst=28
build.class.shade                                                        avg=58 best=91 worst=27
build.class.sharpshot                                                    avg=57 best=73 worst=20
build.class.pyromancer                                                   avg=43 best=54 worst=34
build.class.berserker                                                    avg=74 best=81 worst=66
build.class.phalanx                                                      avg=52 best=76 worst=33
build.class.banneret                                                     avg=12 best=16 worst=5
build.node.shade.reaper-vs-shade.phantom                                 delta=-49
build.node.sharpshot.sniper.onebreath-vs-sharpshot.sniper.killwindow     delta=-48
build.node.bulwark.juggernaut-vs-bulwark.warden                          delta=-44
build.node.phalanx.pikewall-vs-phalanx.lancer                            delta=34
build.node.cleric.warpriest.conflagration-vs-cleric.warpriest.zeal       delta=-21
build.node.sharpshot.sniper-vs-sharpshot.volleyer                        delta=-20
build.node.cleric.warpriest-vs-cleric.lifebinder                         delta=-18
build.node.cleric.lifebinder.greatchorus-vs-cleric.lifebinder.sanctuary  delta=17
run.stable                                                               victory=2 fightwin=73 boss=0.37 gold=16 caps=0
run.fraying                                                              victory=3 fightwin=74 boss=0.51 gold=19 caps=0
run.collapsing                                                           victory=6 fightwin=73 boss=0.46 gold=21 caps=0
build.flags                                                              9
build.flag                                                               CHASSIS-DEAD banneret (best build 16%)
build.flag                                                               DEAD banneret:herald+secondwind+quickening (5%)
build.flag                                                               DEAD banneret:herald+steady+quickening (6%)
build.flag                                                               DEAD banneret:warcaller+drumbeat+bearer (9%)
build.flag                                                               DOMINANT shade:killerstempo+phantom+hereandgone (91%)
build.flag                                                               NODE-LOPSIDED bulwark.juggernaut vs bulwark.warden (Δ-44)
build.flag                                                               NODE-LOPSIDED phalanx.pikewall vs phalanx.lancer (Δ34)
build.flag                                                               NODE-LOPSIDED shade.reaper vs shade.phantom (Δ-49)
build.flag                                                               NODE-LOPSIDED sharpshot.sniper.onebreath vs sharpshot.sniper.killwindow (Δ-48)
health.deadtime-pct                                                      1.79
health.never-swung-pct                                                   0.00
health.frozen-pct                                                        5.00
```
