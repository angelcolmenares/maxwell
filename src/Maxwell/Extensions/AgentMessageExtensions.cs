namespace Maxwell;

public static class AgentMessageExtensions
{
    extension(AgentMessage source)
    {
        public AssistantMessage ToAssistantMessage()
        {
            return new(){ Text = source.Text??string.Empty, Uri= source.Uri};
        }
    }
}