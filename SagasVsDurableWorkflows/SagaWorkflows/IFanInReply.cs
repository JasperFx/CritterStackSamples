namespace SagaWorkflows;

/// <summary>
/// A reply that comes back to a saga together with many others: a file copied, a child workflow finished, a tool call
/// done. Program.cs sends every one of these through global partitioning grouped by saga id, so the replies for one
/// saga are handled one at a time and never fight over the saga document. Different sagas still run in parallel.
/// </summary>
public interface IFanInReply;
