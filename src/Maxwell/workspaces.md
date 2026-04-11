{HOME_DIR}/.maxwell/
└── ws/
    └── 00000000-0000-0000-0000-000000000000/      # default ws
        ├── connections.json                       # settings for all sessions in this ws
        ├── agents.json                            # list of agents with settings for all sessions in this ws
        ├── chats.json                             # [ { id: guid, name: string , leader:string .. } ... ]
        ├── instructions/                          # for all chat sessions in this ws
        │   ├── agent-name.md                      # instructions for agents in this ws
        │   └── other-agent.md
        ├── skills/                                # skills for this ws
        │   ├── agents-skills/
        │   │   └── agent-name/
        │   │       └── say-hello/
        │   │           └── SKILL.md
        │   └── shared-skills/
        │       └── some-skill-name/
        │           └── SKILL.md
        └── chats/
