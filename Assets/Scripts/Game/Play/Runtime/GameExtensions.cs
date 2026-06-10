using UniKit.Framework.Base;

namespace Game.Play
{
    public interface IAccessContext
    {
    }

    public static class GameExtensions
    {
        public static TModel GetModel<TModel>(this IAccessContext owner) where TModel : IModel
            => GameContext.Instance.GetModel<TModel>();

        public static TSystem GetSystem<TSystem>(this IAccessContext owner) where TSystem : ISystem
            => GameContext.Instance.GetSystem<TSystem>();

        public static void SendCommand<T>(this IAccessContext owner, T command) where T : ICommand
            => GameContext.Instance.SendCommand(command);

        public static TResult SendCommand<TResult>(this IAccessContext owner, ICommand<TResult> command)
            => GameContext.Instance.SendCommand(command);
    }
}