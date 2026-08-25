1.1 Dedicated Server: I'm going to buy one dedicated server for each location US, SA, Europe
1.2 World Model: One dedicated server by region
1.3 Concurrency Goal: +1000 connected users per server
2.1 Construction Persistence: The tower pieces are in predefined positions
2.2 Completion Logic: The Process its like 1/10 2/10 and every step will have a different LOD/Asset per stage
2.3 Tower Generation: Procedurally generated for floor tile, pillar, arch, stair. Furniture its hand placed
3. Chunk System: 32m looks like a good number, but its better if i can change to test
3.2 Chunk Ownership: Thread -> Many Chunks
3.3 Frozen Chunks: The building its completed frozen and finished, you can still move along side and interact inside the chunk (besides contruction)
4.1 Character Controller: Unity CharacterController just the basic move, jump, interact
4.2 Movement Type: WASD + Mouse, First Person
4.3 Animation System: Humanoid Mecanim
5.1 Resource Types: It can grow or change so its necessary to be modular
5.2 Resource Flow: They are shared through token / market or another solution, never player to player its always to server throught some interface
5.3 Inflation Control: Resources are used only for construction
6.1 Upgrade Tree Complexity: Small 20 nodes, linear, one level one upgrade kit (passive, active, customization)
6.2 Upgrade Types: yes Speed, Yield, Cost reduction, Visual effects, Social interactions and others types can show in the design phase
6.3 Monetization: One-time purchase
7.1 Chat: Local, when you type something its quickly shown above your head, its not stored just placed -> need filter for swears, moving to others platform and many others for safety
7.2 Groups: You can only add or remove friends to priority list, when an player its on priority list you can observe his actions even in far chunks
7.3 Friends: Just prioritize the shown system, no further interactions
8.1 SQLite Scope: Final production database, the game its not database heavy, its light quickly and dont store personal and sensitivy data, just game data
8.2 Save Frequency: Players NEVER request persistency, the server automaticaly persist the status of the chunk and players every 1 hour
9.1 Tower Assets: For now you will only have one asset for each, you can change the color/texture based on the floor, but will not vary on shape or height, always the same height and shape
9.2 Cosmetics: you will have an interface that you can change the cosmetic, it will be a list of options that you unlock, when you click it swaps, JUST THIS, the simpler the better
10. Testing Requirements: Full coverage -> Unit + Integration + Multiplayer Simulation, every system, every feature should be tested
11.1 MVP Definition: 3 floors, 3 resources(stone, glass, ink), all roles, +50 players, progression
11.2 Final Vision: That will depend of the popularity of the game, i want to have full control of the radius and how many floors
11.3 Development Team: Solo developer

1. FishNet Server Model: Keep the dedicated fishnet server and surpass the high limits with interest and custom playerstate replication. Player disconnects → Character disappears
2. Player Capacity per Chunk: The 20-100 visible players its the limit for local and far chunks, when you hit the limit 100 you stop render and prioritize data, if few players are online you grab far chunks until meet the 100 limit, if a lot of players are connect you stop in the current chunk with 100
3. Player Update Frequency: 10hz network state, 60fps interpolation
4. Construction Contribution: Theres an limit of much you can carry (avoid exploit the share mechanic) you consume your local baggage and then build, you can fill with totem or gather by yourself, and people that gather can store in totem or use from baggage
5. Resource Nodes: Regenerate, the resources nodes are OUTSIDE the tower area, the inside are use just for construction
6. Processing Stations: Shared world stations, Multiple players can use simultaneously
7. Procedural Tower Generation: All the floors are pre generated, the position, rotation its already built in the chunk manager even before the game start
8. Monetization Unlock: You need to buy and Steam DLC to be able to unlock the premium benefits
9. Persistence Risk: Yes you lose 59 minutes of progress, the save rate should be configurable, every 30 minutes, 20 minutes that can be changed, I want to avoid too many database calls
10. Deployment: One executable, RegionConfig.json
11. Future Scope: Only the systems needed to reach launch. Future changes may happen and that will require another design round made by myself