using Microsoft.Teams.AI;
using System.Collections.Concurrent;

namespace Preworkinagent;

/// <summary>
/// Simple in-memory store for conversation histories.
/// For production, consider using a persistent store backed by a database (Redis, CosmosDB, etc.)
/// </summary>
public static class ConversationMemory
{
    private static readonly ConcurrentDictionary<string, List<IMessage>> ConversationStore = new();

    /// <summary>
    /// Get or create conversation memory for a specific conversation
    /// </summary>
    public static List<IMessage> GetOrCreate(string conversationId)
    {
        return ConversationStore.GetOrAdd(conversationId, _ => new List<IMessage>());
    }

    /// <summary>
    /// Clear memory for a specific conversation
    /// </summary>
    public static void Clear(string conversationId)
    {
        if (ConversationStore.TryGetValue(conversationId, out var messages))
        {
            messages.Clear();
        }
    }

    /// <summary>
    /// Remove conversation from store entirely
    /// </summary>
    public static void Remove(string conversationId)
    {
        ConversationStore.TryRemove(conversationId, out _);
    }

    /// <summary>
    /// Get the count of messages in a conversation
    /// </summary>
    public static int GetMessageCount(string conversationId)
    {
        if (ConversationStore.TryGetValue(conversationId, out var messages))
        {
            return messages.Count;
        }
        return 0;
    }
}
