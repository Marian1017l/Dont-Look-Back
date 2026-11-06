using System.Threading;
using System.Threading.Tasks;

namespace Unity.AI.Assistant.FunctionCalling
{
    /// <summary>
    /// An interface to allow user interactions from a tool
    /// </summary>
    public interface IToolInteractions
    {
        /// <summary>
        /// Whether this interaction surface can actually present a request to a user and receive an answer.
        /// False for headless surfaces (external/MCP calls, automated runs) where no dialog can be shown.
        /// </summary>
        bool CanPromptUser => false;

        /// <summary>
        /// Wait for a user interaction
        /// </summary>
        /// <param name="callInfo">The raw tool call data</param>
        /// <param name="userInteraction">The user interaction to wait for</param>
        /// <param name="timeoutSeconds">A duration after which the interaction should fail if not completed</param>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <typeparam name="TOutput">The type of interaction output</typeparam>
        /// <returns>An asynchronous task that returns the interaction result.</returns>
        Task<TOutput> WaitForUser<TOutput>(ToolExecutionContext.CallInfo callInfo, IInteractionSource<TOutput> userInteraction, int timeoutSeconds = 30, CancellationToken cancellationToken = default);
    }
}
