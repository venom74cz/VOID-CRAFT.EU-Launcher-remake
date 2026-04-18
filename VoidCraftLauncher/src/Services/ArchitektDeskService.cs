using System;
using System.ClientModel;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.AI;
using OpenAI;
using VoidCraftLauncher.Models;

namespace VoidCraftLauncher.Services;

/// <summary>
/// Poskytuje unifikované rozhraní pro komunikaci s libovolným OpenAI-compatible AI API
/// přes Microsoft.Extensions.AI. Udržuje uživatelské profily v SecureStorage.
/// </summary>
public sealed class ArchitektDeskService
{
    private const string ProfilesStorageKey = "architekt.profiles";
    private readonly SecureStorageService _secureStorage;

    public ArchitektDeskService(SecureStorageService secureStorage)
    {
        _secureStorage = secureStorage;
    }

    public List<AiProfile> GetProfiles()
    {
        var json = _secureStorage.Get(ProfilesStorageKey);
        if (string.IsNullOrWhiteSpace(json))
            return GetDefaultProfiles();

        try
        {
            var profiles = JsonSerializer.Deserialize<List<AiProfile>>(json);
            if (profiles == null)
                return GetDefaultProfiles();

            // Sanitize keys on load (strip bash ${...} wrappers from copy-paste)
            foreach (var p in profiles)
            {
                if (!string.IsNullOrEmpty(p.ApiKey) && p.ApiKey.StartsWith("${") && p.ApiKey.EndsWith("}"))
                    p.ApiKey = p.ApiKey.Substring(2, p.ApiKey.Length - 3);
            }

            return profiles;
        }
        catch (Exception ex)
        {
            LogService.Error("Failed to deserialize AiProfiles", ex);
            return GetDefaultProfiles();
        }
    }

    public void SaveProfiles(List<AiProfile> profiles)
    {
        // Enforce at least one default
        if (profiles.Count > 0 && !profiles.Any(p => p.IsDefault))
            profiles.First().IsDefault = true;

        var json = JsonSerializer.Serialize(profiles);
        _secureStorage.Set(ProfilesStorageKey, json);
    }

    private List<AiProfile> GetDefaultProfiles()
    {
        return new List<AiProfile>
        {
            new AiProfile
            {
                Name = "OpenAI (Výchozí)",
                BaseUrl = "https://api.openai.com/v1",
                ApiKey = "",
                ModelName = "gpt-4o-mini",
                IsDefault = true
            },
            new AiProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = "OpenCode.ai",
                BaseUrl = "https://opencode.ai/api/v1",
                ApiKey = "",
                ModelName = "default",
                IsDefault = false
            },
            new AiProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Lokální Sít (Ollama / LM Studio)",
                BaseUrl = "http://localhost:11434/v1",
                ApiKey = "dummy-key",
                ModelName = "llama3.2",
                IsDefault = false
            }
        };
    }

    public async IAsyncEnumerable<global::VoidCraftLauncher.Models.ArchitektStreamUpdate> StreamChatAsync(
        string systemPrompt,
        IReadOnlyList<ArchitektMessage> history,
        AiProfile profile,
        string currentWorkspacePath = "",
        bool useTools = true,
        bool isAgentMode = true,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile), "AI Profil není vybrán.");

        if (string.IsNullOrWhiteSpace(profile.BaseUrl))
            throw new ArgumentException("Base URL profilu není definováno.");

        var apiKey = string.IsNullOrWhiteSpace(profile.ApiKey) ? "dummy" : profile.ApiKey;
        var options = new OpenAIClientOptions { Endpoint = new Uri(profile.BaseUrl) };
        var apiClient = new OpenAIClient(new ApiKeyCredential(apiKey), options);
        var chatClient = apiClient.GetChatClient(profile.ModelName);

        var messages = new List<global::OpenAI.Chat.ChatMessage> { new global::OpenAI.Chat.SystemChatMessage(systemPrompt) };
        foreach (var msg in history)
        {
            if (msg.IsUser)
                messages.Add(new global::OpenAI.Chat.UserChatMessage(msg.Content));
            else
                messages.Add(new global::OpenAI.Chat.AssistantChatMessage(msg.Content));
        }

        var chatOptions = new global::OpenAI.Chat.ChatCompletionOptions();
        
        // Define Tools only if requested
        if (useTools)
        {
            chatOptions.Tools.Add(global::OpenAI.Chat.ChatTool.CreateFunctionTool(
                "GetSystemInfo", "Vrátí aktuální informace o HW a OS.",
                BinaryData.FromString("""{"type": "object", "properties": {}}""")
            ));
            chatOptions.Tools.Add(global::OpenAI.Chat.ChatTool.CreateFunctionTool(
                "SearchWeb", "Vyhledá aktuální informace na internetu.",
                BinaryData.FromString("""{"type": "object", "properties": {"query": {"type": "string", "description": "Hledaný termín"}}, "required": ["query"]}""")
            ));

            if (isAgentMode)
            {
                chatOptions.Tools.Add(global::OpenAI.Chat.ChatTool.CreateFunctionTool(
                    "ReadWorkspaceFile", "Přečte obsah lokálního souboru v aktuálním workspace.",
                    BinaryData.FromString("""{"type": "object", "properties": {"relativePath": {"type": "string"}}, "required": ["relativePath"]}""")
                ));
                chatOptions.Tools.Add(global::OpenAI.Chat.ChatTool.CreateFunctionTool(
                    "WriteWorkspaceFile", "Vytvoří nebo přepíše soubor v aktuálním workspace.",
                    BinaryData.FromString("""{"type": "object", "properties": {"relativePath": {"type": "string"}, "content": {"type": "string"}}, "required": ["relativePath", "content"]}""")
                ));
                chatOptions.Tools.Add(global::OpenAI.Chat.ChatTool.CreateFunctionTool(
                    "ListWorkspaceDirectory", "Vypíše obsah složky (soubory a podsložky) v aktuálním workspace.",
                    BinaryData.FromString("""{"type": "object", "properties": {"relativePath": {"type": "string", "description": "Relativní cesta ke složce (prázdný string = root workspace)"}}, "required": ["relativePath"]}""")
                ));
            }
        }

        LogService.Log($"ARCHITEKT: Sending to {profile.BaseUrl} model={profile.ModelName} tools={useTools} msgs={messages.Count}");

        bool requiresAnotherRun = true;
        int iterationCount = 0;
        const int maxIterations = 8;

        while (requiresAnotherRun && iterationCount < maxIterations)
        {
            iterationCount++;
            requiresAnotherRun = false;
            
            var toolCallIdBuilder = new Dictionary<int, string>();
            var functionNameBuilder = new Dictionary<int, string>();
            var functionArgBuilder = new Dictionary<int, System.Text.StringBuilder>();
            var assistantTextBuilder = new System.Text.StringBuilder();

            await foreach (var update in chatClient.CompleteChatStreamingAsync(messages, chatOptions, cancellationToken))
            {
                if (update.ToolCallUpdates != null && update.ToolCallUpdates.Count > 0)
                {
                    requiresAnotherRun = true;
                    foreach (var tu in update.ToolCallUpdates)
                    {
                        if (tu.ToolCallId != null) toolCallIdBuilder[tu.Index] = tu.ToolCallId;
                        if (tu.FunctionName != null) 
                        {
                            functionNameBuilder[tu.Index] = tu.FunctionName;
                            yield return new global::VoidCraftLauncher.Models.ArchitektStreamUpdate { IsStatus = true, Text = $"Nástroj: {tu.FunctionName}..." };
                        }
                        if (tu.FunctionArgumentsUpdate != null)
                        {
                            if (!functionArgBuilder.ContainsKey(tu.Index)) functionArgBuilder[tu.Index] = new System.Text.StringBuilder();
                            functionArgBuilder[tu.Index].Append(tu.FunctionArgumentsUpdate.ToString());
                        }
                    }
                }

                foreach (var part in update.ContentUpdate)
                {
                    if (part.Text != null)
                    {
                        assistantTextBuilder.Append(part.Text);
                        var yieldUpd = new global::VoidCraftLauncher.Models.ArchitektStreamUpdate { IsStatus = false, Text = part.Text };
                        yield return yieldUpd;
                    }
                }
            }

            LogService.Log($"ARCHITEKT: Iteration done. TextLen={assistantTextBuilder.Length}, Tools={functionNameBuilder.Count}");

            if (requiresAnotherRun)
            {
                var toolCalls = new List<global::OpenAI.Chat.ChatToolCall>();
                foreach (var kvp in functionNameBuilder)
                {
                    var id = toolCallIdBuilder.ContainsKey(kvp.Key) ? toolCallIdBuilder[kvp.Key] : $"call_{Guid.NewGuid()}";
                    var name = kvp.Value;
                    var args = functionArgBuilder.ContainsKey(kvp.Key) ? functionArgBuilder[kvp.Key].ToString() : "{}";
                    toolCalls.Add(global::OpenAI.Chat.ChatToolCall.CreateFunctionToolCall(id, name, BinaryData.FromString(args)));
                }

                string assistantText = assistantTextBuilder.ToString();
                global::OpenAI.Chat.AssistantChatMessage assistantMsg;

                if (string.IsNullOrEmpty(assistantText))
                {
                    // Některé platformy (Gemini, Llama na Groqu) vyžadují null/empty content u zpráv s ToolCalls
                    assistantMsg = new global::OpenAI.Chat.AssistantChatMessage(toolCalls);
                }
                else
                {
                    assistantMsg = new global::OpenAI.Chat.AssistantChatMessage(assistantText);
                    foreach (var tc in toolCalls)
                    {
                        assistantMsg.ToolCalls.Add(tc);
                    }
                }

                messages.Add(assistantMsg);
                
                // Krátká pauza pro stabilitu Rate Limitů (Groq free tier) před voláním nástrojů
                await Task.Delay(500, cancellationToken);
                
                // Execute tools
                foreach (var tCall in toolCalls)
                {
                    string result = "Neznámý nástroj";
                    try
                    {
                        var argsDoc = JsonDocument.Parse(tCall.FunctionArguments);
                        if (tCall.FunctionName == "GetSystemInfo")
                        {
                            result = global::VoidCraftLauncher.Agent.ArchitektAgentTools.GetSystemInfo();
                        }
                        else if (tCall.FunctionName == "SearchWeb")
                        {
                            var query = argsDoc.RootElement.TryGetProperty("query", out var qObj) ? qObj.GetString() ?? "" : "";
                            result = await global::VoidCraftLauncher.Agent.ArchitektAgentTools.SearchWebAsync(query);
                        }
                        else if (tCall.FunctionName == "ReadWorkspaceFile")
                        {
                            var relPath = argsDoc.RootElement.TryGetProperty("relativePath", out var pObj) ? pObj.GetString() ?? "" : "";
                            result = global::VoidCraftLauncher.Agent.ArchitektAgentTools.ReadWorkspaceFile(relPath, currentWorkspacePath);
                        }
                        else if (tCall.FunctionName == "WriteWorkspaceFile")
                        {
                            var relPath = argsDoc.RootElement.TryGetProperty("relativePath", out var pObj) ? pObj.GetString() ?? "" : "";
                            var content = argsDoc.RootElement.TryGetProperty("content", out var cObj) ? cObj.GetString() ?? "" : "";
                            result = global::VoidCraftLauncher.Agent.ArchitektAgentTools.WriteWorkspaceFile(relPath, content, currentWorkspacePath);
                        }
                        else if (tCall.FunctionName == "ListWorkspaceDirectory")
                        {
                            var relPath = argsDoc.RootElement.TryGetProperty("relativePath", out var pObj) ? pObj.GetString() ?? "" : "";
                            result = global::VoidCraftLauncher.Agent.ArchitektAgentTools.ListWorkspaceDirectory(relPath, currentWorkspacePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        result = $"Chyba při volání nástroje: {ex.Message}";
                    }

                    LogService.Log($"ARCHITEKT: Tool {tCall.FunctionName} result len: {result?.Length ?? 0}");
                    messages.Add(new global::OpenAI.Chat.ToolChatMessage(tCall.Id, result ?? "Neznámá chyba"));
                }
            }
        }
        
        if (iterationCount >= maxIterations)
        {
            LogService.Log($"ARCHITEKT: Reached max iterations ({maxIterations}). Stopping tool loop.");
        }
    }
}
