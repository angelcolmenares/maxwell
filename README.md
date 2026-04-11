# Maxwell

Maxwell is an experimental project designed to play with and explore the capabilities of the **Microsoft Agent Framework**. It provides a structured environment (workspaces) to orchestrate multiple agents, manage their skills, and maintain persistent chat sessions with specific configurations.

## 🚀 Overview

This repository serves as a playground for testing agent orchestration, skill implementation, and workspace management within the Microsoft Agent Framework ecosystem. It demonstrates how to define agents with specific roles, attach shared or agent-specific skills, and manage connections to various LLM providers (like OpenAI or local endpoints).

## 📁 Workspace Structure

To use Maxwell, you must set up a workspace following the structure defined in the project. A workspace is located within your home directory under `.maxwell/ws/`. 

Each workspace is identified by a unique GUID. Below is the required directory structure:

```text
{HOME_DIR}/.maxint/.maxwell/
└── ws/
    └── [WORKSPACE_GUID]/      # Create a directory with a unique GUID
        ├── connections.json   # Configuration for LLM connections (e.g., OpenAI, Local)
        ├── agents.json        # Definition of agents, their models, and roles
        ├── chats.json         # Registry of chat sessions
        ├── instructions/      # Markdown files containing system instructions for agents
        │   ├── [agent-name].md
        │   └── ...
        ├── skills/            # Reusable skills for the workspace
        │   ├── agents-skills/ # Skills specific to individual agents
        │   │   └── [agent-name]/
        │   │       └── [skill-name]/
        │   │           └── SKILL.md
        │   └── shared-skills/ # Skills shared across the entire workspace
        │       └── [skill-name]/
        │           └── SKILL.md
        └── chats/             # Directory for chat-specific data/logs
```

## 🛠️ Configuration & Templates

Maxwell relies on JSON configuration files to define its operational environment. You can use the templates provided in the `src/Maxwell/templates` directory to create your own workspace files.

### 1. Agents Configuration (`agents.json`)
Define your agents here. Each entry includes the model to be used, the connection name, a description, and the agent's role (e.g., `Leader`, `Assistant`, `ToolSelector`).
*   **Template available:** `src/Maxwell/templates/agents.json`

### 2. Connections Configuration (`connections.json`)
Specify how agents connect to LLM providers. You can define local endpoints (e.g., `openai-local`) or production APIs.
*   **Template available:** `src/Maxwell/templates/connections.json`

### 3. Chat Registry (`chats.json`)
Manage your chat session history and metadata.
*   **Template available:** `src/Maxwell/templates/chats.json`

## ⚙️ Key Features

*   **Multi-Agent Orchestration:** Define a hierarchy of agents, including leaders and specialized assistants.
*   **Modular Skills:** Implement skills that can be either private to an agent or shared across the workspace.
*   **Flexible Connections:** Easily switch between local LLM providers and cloud-based APIs.
*   **Structured Instructions:** Use Markdown files within the workspace to provide clear, versionable instructions to your agents.

## 🤝 Contributing

Feel free to:

    Report issues

    Suggest improvements

    Add examples

## 🙏 Acknowledgements

A special thanks to [Rasmus Wulff Jensen](https://github.com/rwjdk) for the amazing [AgentFrameworkToolkit](https://github.com/rwjdk/AgentFrameworkToolkit) and for the videos on his [YouTube channel](https://www.youtube.com/@rwj_dk).

## ⚖️ License

This project is provided as-is for educational and research purposes.