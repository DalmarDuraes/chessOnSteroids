using UnityEngine.SceneManagement;

namespace Chess.Multiplayer
{
    /// <summary>
    /// Resolves Photon lobby vs gameplay scenes from Editor build order (PhotonScene → 0, MainScene → 1).
    /// </summary>
    public static class ChessFusionBuildIndices
    {
        public const string PhotonScenePath = "Assets/Scenes/PhotonScene.unity";
        public const string GameplayScenePath = "Assets/Scenes/MainScene.unity";

        static int? _photon;
        static int? _gameplay;

        public static int PhotonScene => Resolve(ref _photon, PhotonScenePath);
        public static int GameplayScene => Resolve(ref _gameplay, GameplayScenePath);

        static int Resolve(ref int? cache, string path)
        {
            if (cache.HasValue)
                return cache.Value;
            int i = SceneUtility.GetBuildIndexByScenePath(path);
            cache = i;
            return i;
        }
    }
}
