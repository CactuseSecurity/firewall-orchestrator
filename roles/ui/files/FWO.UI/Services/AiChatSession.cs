using System.Security.Claims;
using FWO.Ai.Client;
using FWO.Data;
using FWO.Logging;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.AI;

namespace FWO.Ui.Services
{
    /// <summary>
    /// The AI chat of a UI session (Blazor circuit): the conversation with the FWO agent, the runs on the AI harness, the
    /// tool calls waiting for the user's approval, and the state of the chat panel. The conversation lives only in the
    /// memory of the circuit: it survives navigating between pages, and is lost on reload, logout or "new chat". The
    /// harness is stateless, so every run sends the conversation the harness returned at the end of the previous run,
    /// followed by the new message or the answers to the approval requests (see <see cref="AgentConversation"/>). Each
    /// run is sent with the access token of the session, so the harness applies the roles of the user. The conversation
    /// is dropped when another user (or no user) is logged in to the circuit.
    /// </summary>
    /// <remarks>
    /// Runs go on in the background, also while the panel is minimized. <see cref="Changed"/> is raised from the thread of
    /// the run, so components re-render through their dispatcher. The entries are changed only while holding the lock.
    /// </remarks>
    public sealed class AiChatSession : IDisposable
    {
        /// <summary>Maximum length of a message of the user.</summary>
        public const int kMaxMessageLength = 10000;

        /// <summary>Width of the chat panel in pixels until the user resizes it.</summary>
        public const int kDefaultPanelWidth = 420;

        /// <summary>Smallest width of the chat panel in pixels.</summary>
        public const int kMinPanelWidth = 300;

        /// <summary>Largest width of the chat panel in pixels.</summary>
        public const int kMaxPanelWidth = 1600;

        /// <summary>Shortest time between two notifications of a streamed answer.</summary>
        internal const long kNotifyIntervalMilliseconds = 150;

        /// <summary>Claim of the JWT holding the distinguished name of the user.</summary>
        internal const string kUserDnClaim = "x-hasura-uuid";

        private const string kLogTitle = "AI Chat";

        private readonly IFwoAgentChatClientFactory chatClientFactory;
        private readonly IAccessTokenSource accessTokenSource;
        private readonly AuthenticationStateProvider authenticationStateProvider;
        private readonly object stateLock = new();
        private readonly List<AiChatEntry> entries = [];
        private List<ChatMessage> conversation = [];
        private string? threadId;
        private string? conversationUserDn;
        private AiChatEntry? pendingApprovalEntry;
        private CancellationTokenSource? runCancellation;
        private long conversationGeneration;
        private long lastNotifyMilliseconds;
        private int panelWidth = kDefaultPanelWidth;
        private bool running;
        private bool isOpen;
        private bool hasNewAnswer;
        private bool disposed;

        /// <summary>
        /// Creates the chat of a UI session.
        /// </summary>
        /// <param name="chatClientFactory">Creates the chat clients on the AG-UI endpoint of the harness</param>
        /// <param name="accessTokenSource">Provides the access token of the session for each run</param>
        /// <param name="authenticationStateProvider">Authentication state of the session, to drop the conversation when
        /// the user changes</param>
        public AiChatSession(IFwoAgentChatClientFactory chatClientFactory, IAccessTokenSource accessTokenSource,
            AuthenticationStateProvider authenticationStateProvider)
        {
            this.chatClientFactory = chatClientFactory;
            this.accessTokenSource = accessTokenSource;
            this.authenticationStateProvider = authenticationStateProvider;
            authenticationStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
        }

        /// <summary>
        /// Raised when the chat changed: an entry, the state of a run, or the state of the panel. May be raised from the
        /// thread of a run.
        /// </summary>
        public event Action? Changed;

        /// <summary>Whether the chat panel is open.</summary>
        public bool IsOpen
        {
            get { lock (stateLock) { return isOpen; } }
        }

        /// <summary>Whether an answer arrived while the panel was minimized; cleared when the panel is opened.</summary>
        public bool HasNewAnswer
        {
            get { lock (stateLock) { return hasNewAnswer; } }
        }

        /// <summary>Whether a run is going on.</summary>
        public bool IsRunning
        {
            get { lock (stateLock) { return running; } }
        }

        /// <summary>Whether the last answer waits until the user approved or rejected its tool calls.</summary>
        public bool WaitsForApproval
        {
            get { lock (stateLock) { return pendingApprovalEntry != null; } }
        }

        /// <summary>Whether a new message can be sent: no run is going on and no tool call waits for approval.</summary>
        public bool CanSend
        {
            get { lock (stateLock) { return !running && pendingApprovalEntry == null; } }
        }

        /// <summary>Width of the chat panel in pixels, kept between <see cref="kMinPanelWidth"/> and <see cref="kMaxPanelWidth"/>.</summary>
        public int PanelWidth
        {
            get { lock (stateLock) { return panelWidth; } }
            set
            {
                int width = Math.Clamp(value, kMinPanelWidth, kMaxPanelWidth);
                lock (stateLock)
                {
                    if (panelWidth == width)
                    {
                        return;
                    }
                    panelWidth = width;
                }
                NotifyChanged();
            }
        }

        /// <summary>The message the user is writing, kept while the panel is minimized.</summary>
        public string Draft { get; set; } = "";

        /// <summary>
        /// Returns the entries of the conversation, oldest first.
        /// </summary>
        public List<AiChatEntry> GetEntries()
        {
            lock (stateLock)
            {
                return [.. entries];
            }
        }

        /// <summary>
        /// Opens the chat panel.
        /// </summary>
        public void Open()
        {
            SetOpen(true);
        }

        /// <summary>
        /// Minimizes the chat panel; a run goes on.
        /// </summary>
        public void Minimize()
        {
            SetOpen(false);
        }

        /// <summary>
        /// Opens the chat panel if it is minimized, else minimizes it.
        /// </summary>
        public void Toggle()
        {
            SetOpen(!IsOpen);
        }

        /// <summary>
        /// Sends a message of the user and runs the agent with it. Does nothing for a blank or too long message, while a
        /// run is going on, or while a tool call waits for approval. Returns when the run has ended; its outcome is shown
        /// in the entry of the answer.
        /// </summary>
        /// <param name="text">The message</param>
        public async Task SendAsync(string? text)
        {
            string message = text?.Trim() ?? "";
            if (message.Length == 0 || message.Length > kMaxMessageLength)
            {
                return;
            }
            string userDn = await GetUserDnAsync();
            List<ChatMessage> messages;
            long generation;
            lock (stateLock)
            {
                if (disposed || running || pendingApprovalEntry != null)
                {
                    return;
                }
                if (conversationUserDn != null && !IsSameUser(conversationUserDn, userDn))
                {
                    ClearConversationLocked();
                }
                conversationUserDn ??= userDn;
                entries.Add(new AiChatEntry(AiChatEntryKind.User, message));
                messages = [.. conversation, new ChatMessage(ChatRole.User, message)];
                running = true;
                generation = conversationGeneration;
            }
            Draft = "";
            await RunAsync(messages, generation);
        }

        /// <summary>
        /// Answers a tool call waiting for approval. When all tool calls of the answer are answered, the agent runs on
        /// with the answers: an approved call is run, a rejected one is not.
        /// </summary>
        /// <param name="requestId">Id of the approval request</param>
        /// <param name="approved">Whether the user approved the call</param>
        public async Task AnswerApprovalAsync(string requestId, bool approved)
        {
            List<ChatMessage>? messages = null;
            long generation;
            lock (stateLock)
            {
                AiChatApproval? approval = pendingApprovalEntry?.Approvals
                    .FirstOrDefault(pendingApproval => pendingApproval.RequestId == requestId && pendingApproval.Approved == null);
                if (disposed || running || pendingApprovalEntry == null || approval == null)
                {
                    return;
                }
                approval.Approved = approved;
                generation = conversationGeneration;
                if (pendingApprovalEntry.Approvals.All(pendingApproval => pendingApproval.Approved != null))
                {
                    messages = CreateApprovalAnswerMessagesLocked(pendingApprovalEntry);
                    pendingApprovalEntry.State = AiChatAnswerState.Completed;
                    pendingApprovalEntry = null;
                    running = true;
                }
            }
            if (messages == null)
            {
                NotifyChanged();
                return;
            }
            await RunAsync(messages, generation);
        }

        /// <summary>
        /// Cancels the run going on; the conversation stays as it was before the run.
        /// </summary>
        public void Cancel()
        {
            CancellationTokenSource? cancellation;
            lock (stateLock)
            {
                cancellation = runCancellation;
            }
            CancelRun(cancellation);
        }

        /// <summary>
        /// Starts a new conversation: cancels the run going on and drops all entries and the conversation.
        /// </summary>
        public void StartNewChat()
        {
            CancellationTokenSource? cancellation;
            lock (stateLock)
            {
                cancellation = runCancellation;
                ClearConversationLocked();
            }
            CancelRun(cancellation);
            NotifyChanged();
        }

        /// <summary>
        /// Cancels the run going on and stops following the authentication state.
        /// </summary>
        public void Dispose()
        {
            CancellationTokenSource? cancellation;
            lock (stateLock)
            {
                if (disposed)
                {
                    return;
                }
                disposed = true;
                cancellation = runCancellation;
            }
            authenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
            CancelRun(cancellation);
            Changed = null;
        }

        /// <summary>
        /// Runs the agent with the given messages and applies the outcome to a new entry of the answer. The caller has
        /// set <see cref="running"/>. A run of a conversation dropped in the meantime (see <see cref="conversationGeneration"/>)
        /// changes nothing.
        /// </summary>
        private async Task RunAsync(List<ChatMessage> messages, long generation)
        {
            AiChatEntry answer = new(AiChatEntryKind.Assistant);
            using CancellationTokenSource cancellation = new();
            string? runThreadId;
            lock (stateLock)
            {
                entries.Add(answer);
                runCancellation = cancellation;
                runThreadId = threadId;
            }
            NotifyChanged();
            AiChatRun run = new();
            try
            {
                await StreamRunAsync(messages, runThreadId, run, answer, cancellation.Token);
                lock (stateLock)
                {
                    if (generation == conversationGeneration)
                    {
                        CompleteLocked(answer, run, messages);
                    }
                }
            }
            catch (Exception) when (cancellation.IsCancellationRequested)
            {
                SetOutcome(answer, run, AiChatAnswerState.Cancelled, null);
            }
            catch (AgentClientException exception)
            {
                LogClientFailure(exception);
                SetOutcome(answer, run, AiChatAnswerState.Failed, exception);
            }
            catch (Exception exception)
            {
                Log.WriteError(kLogTitle, "The run of the AI chat failed.", exception);
                SetOutcome(answer, run, AiChatAnswerState.Failed, null);
            }
            finally
            {
                EndRun(cancellation, generation);
            }
        }

        /// <summary>
        /// Streams the run, showing the answer text in the entry while it grows.
        /// </summary>
        private async Task StreamRunAsync(List<ChatMessage> messages, string? runThreadId, AiChatRun run, AiChatEntry answer,
            CancellationToken cancellationToken)
        {
            IChatClient chatClient = chatClientFactory.CreateChatClient(_ => accessTokenSource.GetAccessToken());
            ChatOptions options = new() { ConversationId = runThreadId };
            await foreach (ChatResponseUpdate update in chatClient.GetStreamingResponseAsync(messages, options, cancellationToken))
            {
                if (run.Apply(update))
                {
                    lock (stateLock)
                    {
                        answer.Text = run.Text;
                    }
                    NotifyThrottled();
                }
            }
        }

        /// <summary>
        /// Applies the outcome of a run that ended: its error, the tool calls waiting for approval, or its answer. The
        /// conversation of the chat becomes the one the harness returned; it stays as before for a failed run.
        /// </summary>
        private void CompleteLocked(AiChatEntry answer, AiChatRun run, List<ChatMessage> messages)
        {
            threadId = run.ThreadId ?? threadId;
            answer.Text = run.Text;
            if (run.Error != null)
            {
                Log.WriteWarning(kLogTitle, $"The run on thread {run.ThreadId} failed: {run.Error.ErrorCode} {run.Error.Message}");
                answer.State = AiChatAnswerState.Failed;
            }
            else if (run.ApprovalRequests.Count > 0)
            {
                CompleteWithApprovalsLocked(answer, run);
            }
            else if (string.IsNullOrWhiteSpace(run.Text))
            {
                Log.WriteWarning(kLogTitle, $"The run on thread {run.ThreadId} completed without an answer.");
                answer.State = AiChatAnswerState.Failed;
            }
            else
            {
                conversation = run.Conversation ?? [.. messages, new ChatMessage(ChatRole.Assistant, run.Text)];
                answer.State = AiChatAnswerState.Completed;
            }
        }

        /// <summary>
        /// Lets the answer wait for the user's approval of its tool calls. The run must have returned the conversation,
        /// as the answers are sent after it.
        /// </summary>
        private void CompleteWithApprovalsLocked(AiChatEntry answer, AiChatRun run)
        {
            if (run.Conversation == null)
            {
                Log.WriteWarning(kLogTitle, $"The run on thread {run.ThreadId} asked for approvals without returning the conversation.");
                answer.State = AiChatAnswerState.Failed;
                return;
            }
            conversation = run.Conversation;
            answer.Approvals = run.ApprovalRequests.Select(request => new AiChatApproval(request)).ToList();
            answer.State = AiChatAnswerState.WaitingForApproval;
            pendingApprovalEntry = answer;
        }

        /// <summary>
        /// Returns the messages of the run answering the approval requests: the conversation followed by one user message
        /// with the answers.
        /// </summary>
        private List<ChatMessage> CreateApprovalAnswerMessagesLocked(AiChatEntry approvalEntry)
        {
            List<AIContent> answers = approvalEntry.Approvals
                .Select(approval => (AIContent)approval.Request.CreateResponse(approval.Approved == true))
                .ToList();
            return [.. conversation, new ChatMessage(ChatRole.User, answers)];
        }

        /// <summary>
        /// Sets the outcome of a run that was cancelled or failed, keeping the text received so far.
        /// </summary>
        private void SetOutcome(AiChatEntry answer, AiChatRun run, AiChatAnswerState state, AgentClientException? clientFailure)
        {
            lock (stateLock)
            {
                answer.Text = run.Text;
                answer.State = state;
                answer.ClientFailure = clientFailure;
            }
        }

        /// <summary>
        /// Ends a run: the next message can be sent, and an answer arriving while the panel is minimized is marked as new.
        /// </summary>
        private void EndRun(CancellationTokenSource cancellation, long generation)
        {
            lock (stateLock)
            {
                if (runCancellation == cancellation)
                {
                    runCancellation = null;
                }
                if (generation == conversationGeneration)
                {
                    running = false;
                    hasNewAnswer = hasNewAnswer || !isOpen;
                }
            }
            NotifyChanged();
        }

        /// <summary>
        /// Drops the entries and the conversation; a run going on no longer changes anything.
        /// </summary>
        private void ClearConversationLocked()
        {
            entries.Clear();
            conversation = [];
            threadId = null;
            pendingApprovalEntry = null;
            conversationUserDn = null;
            runCancellation = null;
            running = false;
            hasNewAnswer = false;
            conversationGeneration++;
        }

        /// <summary>
        /// Opens or minimizes the panel; opening clears the mark of a new answer.
        /// </summary>
        private void SetOpen(bool open)
        {
            lock (stateLock)
            {
                if (isOpen == open)
                {
                    return;
                }
                isOpen = open;
                if (open)
                {
                    hasNewAnswer = false;
                }
            }
            NotifyChanged();
        }

        /// <summary>
        /// Drops the conversation and minimizes the panel when another user, or no user, is logged in to the circuit.
        /// The handler of the provider returns void, so the work is started and every failure is logged.
        /// </summary>
        private void OnAuthenticationStateChanged(Task<AuthenticationState> authenticationStateTask)
        {
            _ = ResetIfOtherUserAsync(authenticationStateTask);
        }

        /// <summary>
        /// Drops the conversation if it belongs to another user than the one now logged in.
        /// </summary>
        private async Task ResetIfOtherUserAsync(Task<AuthenticationState> authenticationStateTask)
        {
            try
            {
                AuthenticationState authenticationState = await authenticationStateTask;
                string userDn = GetUserDn(authenticationState.User);
                bool otherUser;
                lock (stateLock)
                {
                    otherUser = conversationUserDn != null && !IsSameUser(conversationUserDn, userDn);
                }
                if (otherUser)
                {
                    StartNewChat();
                    Minimize();
                }
            }
            catch (Exception exception)
            {
                Log.WriteWarning(kLogTitle, $"Could not check the user of the AI chat after an authentication change: {exception.Message}");
            }
        }

        /// <summary>
        /// Returns the distinguished name of the user logged in to the circuit; empty if there is none.
        /// </summary>
        private async Task<string> GetUserDnAsync()
        {
            AuthenticationState authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();
            return GetUserDn(authenticationState.User);
        }

        /// <summary>
        /// Returns the distinguished name of the user; empty if the user has none.
        /// </summary>
        internal static string GetUserDn(ClaimsPrincipal user)
        {
            return user.FindFirstValue(kUserDnClaim) ?? "";
        }

        /// <summary>
        /// Returns whether two distinguished names name the same user; two empty names count as the same.
        /// </summary>
        internal static bool IsSameUser(string leftDn, string rightDn)
        {
            if (string.IsNullOrWhiteSpace(leftDn) || string.IsNullOrWhiteSpace(rightDn))
            {
                return string.IsNullOrWhiteSpace(leftDn) && string.IsNullOrWhiteSpace(rightDn);
            }
            return DistName.DnEquals(leftDn, rightDn);
        }

        /// <summary>
        /// Logs a run that could not be done: a failure the user can solve as warning, all others as error.
        /// </summary>
        private static void LogClientFailure(AgentClientException exception)
        {
            string logText = $"The run failed ({exception.Failure}, status {exception.StatusCode?.ToString() ?? "-"}): {exception.Message}";
            if (AiReasonCheckDisplay.IsUserSolvable(exception.Failure))
            {
                Log.WriteWarning(kLogTitle, logText);
            }
            else
            {
                Log.WriteError(kLogTitle, logText, exception);
            }
        }

        /// <summary>
        /// Cancels a run; a run that has just ended may have disposed its cancellation already.
        /// </summary>
        private static void CancelRun(CancellationTokenSource? cancellation)
        {
            try
            {
                cancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // the run has ended in the meantime
            }
        }

        /// <summary>
        /// Notifies the components, at most every <see cref="kNotifyIntervalMilliseconds"/> while an answer is streamed.
        /// </summary>
        private void NotifyThrottled()
        {
            long now = Environment.TickCount64;
            if (now - Interlocked.Read(ref lastNotifyMilliseconds) < kNotifyIntervalMilliseconds)
            {
                return;
            }
            Interlocked.Exchange(ref lastNotifyMilliseconds, now);
            NotifyChanged();
        }

        /// <summary>
        /// Notifies the components that the chat changed.
        /// </summary>
        private void NotifyChanged()
        {
            try
            {
                Changed?.Invoke();
            }
            catch (Exception exception)
            {
                Log.WriteDebug(kLogTitle, $"A component could not be notified of a change of the AI chat: {exception.Message}");
            }
        }
    }
}
