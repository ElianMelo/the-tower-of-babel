namespace TowerOfBabel.Resources.Interaction
{
    public enum InteractionActionKind { None, Gather, Process, Build }

    /// <summary>Presentation of the current action, using the same upgraded quantities as gameplay.</summary>
    public readonly struct InteractionResourceData
    {
        public InteractionActionKind Action { get; }
        public ResourceType? Source { get; }
        public ResourceType? Target { get; }
        public int SourceAmount { get; }
        public int TargetAmount { get; }

        private InteractionResourceData(InteractionActionKind action,ResourceType? source,int sourceAmount,ResourceType? target,int targetAmount)
        { Action=action; Source=source; SourceAmount=sourceAmount; Target=target; TargetAmount=targetAmount; }

        public static InteractionResourceData Gather(ResourceType resource,int amount) => new(InteractionActionKind.Gather,null,0,resource,amount);
        public static InteractionResourceData Process(ResourceType source,int sourceAmount,ResourceType target,int targetAmount) => new(InteractionActionKind.Process,source,sourceAmount,target,targetAmount);
        public static InteractionResourceData Build(ResourceType resource,int amount) => new(InteractionActionKind.Build,resource,amount,null,0);
    }
}
