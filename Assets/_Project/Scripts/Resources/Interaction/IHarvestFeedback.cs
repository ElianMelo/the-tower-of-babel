namespace TowerOfBabel.Resources.Interaction
{
    /// <summary>Local gathering presentation driven by <see cref="Resource"/>; never grants resources.</summary>
    public interface IHarvestFeedback
    {
        void BeginWork();
        void SetProgress(float value);
        void StopWork();
        void Deplete();
        void Respawn();
        void ResetFeedback();
    }
}
