Tower of Babel - Social MMO Incremental

Overview: It's a mmo about construction of the tower of babel, you can perform activies like gather resources like stones, ink, sand, processing thoses resouces transforming sand into glass, stone into processed stone, ink into proper paint material, and the process of building the tower itself, the tower starts with a large base and every floor the range of the base get smaller, the tower has 5 main assets: floor tile, pillar, arch, stairs and intern furniture, the world its built to have millions of those assets across many chunks. Players can interact with each other with temporary chat that appers over their heads, emotes, character customization, interact with props that changes small space around them with particle or others world effects, you can see many nearby players at the same time. Players gain experience by performing activities like gather, processing, building, and theres an upgrade tree that unlocks cosmetics and enhance the profeciency of those activities

Tools Requirements
- Engine: Unity 6.3 LTS 6000.3.11f1 URP
- Coding: Visual Studio
- Network: FishNet Networking Evolved 4.7.2R
- Database: SQLite
- Code Library: NaughtyAttributes 2.1.6, Cinemachine 3.1.7, Input System 1.19.0, Multiplayer Center 1.0.1, Test Framework 1.6.0, uGUI (Unity UI) 2.0.0, Shader Graph 17.3.0

Software Requirements
- Use uGUI Unity UI for interfaces
- Always make features along side automate tests
- Focus on Game Development specific design patterns and good pratices
- Every network code should be optimize to +1000 concurrent users
- Every features should be planned and approve before starting

Game Loop
Roles are not determined like classes they are the determined by how much of that activity you perform, each role has his own upgrade tree to unlock cosmetics and emotes of that class, but you dont need to choose from a interface, if you gather resource ou gain Gather experience, if you build an pillar you gain Builder experience, you can choose the same role or change by your own will
- Choose and role: Gather, Processor, Builder
- Perform the activity of that role
- Share resources and interact with others players
- Repeat

Optimization
- Every data should be stored in an struct format to update players through network
- The world would be separated in chunks and loaded by demand
- The height of the chunk its the size of the floor and the depth and width are fixed values
- The current chunk are made of game objects and far chunks are rendered in the GPU
- You can see over 20 to 100 players at the same time ordered by the current chunk and the max amount of players
- You can also prioritize friends to always be rendered
- Avoid NetworkTransform and NetworkAnimator the net code should be optimize to handle +1000 concurrent uses
- Every player send data to the server
- Only few players are notified by the server of neaby players actions (Avoid 1000x1000 Server and Client RPC)
- Players send PlayerState with data like position, rotation and other frequent and relevant information
- Players send Requests to server to trigger interactions like emote, dance, chat
- When player interact with builder the chunk should store the progress of that object
- Data can be compacted to byte if necessary to meet the 1000 concurrent users metric
- The database will be updated with chunk and players data
- Every chunk will have its own thread, when the thred reachs its limit you can allocate more chunks in the same thread
- NEVER will be needed to handle racing conditions, when you change one chunk, every operation on that chunk will be handled by the same thread

Progression
- Each role has its own upgrade tree
- The max free level its 20, the max paid level its 50
- Every level you unlock active upgrades that enhance the eficiency of that role and passive upgrade that unlocks emote, dance, customization
- After the level 20 the progression its much slower in an linear way

Gameplay
- The exterior part of the tower have places to gather resource and process those resouces
- Every time an floor its finished those tools will be avaliable inside that floor
- To Colect: Click and Wait (will be changed for a better design in the future) will give you X amount of that resource
- To Process: Click and Wait (will be changed for a better design in the future) will process X amount of that resource
- To Build: Click and Wait (will be changed for a better design in the future) will progress X percentage of that asset
- Interface: Whell of actions for emote, dance
- Interface: Line to type the chat
- Interface: Share resources, select x amount
- NOTHING related to inventory like drag n drop, slots, fixed amount
- You have x Integer of that resource, thats stored in your player data, and can be used to share in a totem or similar
- Resources, experience, upgrade tree state, level, name, customization that should be stored in the server and persisted in an database
- Everything should be request to the server (fishnet), and the server (fishnet) will handle the local database (SQL Lite)

Systems
- To handle chunk changes, you can only observe closely and frequently the current chunk, the nearby chunks are updated with an less frequency, when an chunk its completed the data its frozen and any more requests are made about that chunk to the server
- To handle the nearby players, you have a list of priority based on chunk and friends, those observed players you will receive more frequent updates about theys PlayerState and Requests so you can built thoses updates into PlayerAnimator, chat and others interactions
- To handle the local player, you will send data to the server using an generic system that receives data from every players you should be able to perform the interactions and that will update an local controller to perform the visuals for self
- to handle the player upgrades, when gaining experience you can progress through the level rewards, you will need an system to send and read related that with the server (fishnet)