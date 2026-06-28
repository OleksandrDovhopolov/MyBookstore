using VContainer;

namespace Game.Bootstrap
{
    // Scene scope для LocationScene (additive). Родитель — GlobalLifetimeScope, задаётся в рантайме
    // через LifetimeScope.EnqueueParent(global) в GameFlowService (parentReference в инспекторе пустой).
    //
    // Наследует BaseLifetimeScope (как GameplayLifetimeScope) — именно он даёт в инспекторе поля
    // "Mono Installers" / "Scriptable Object Installers" и прогоняет их. Сюда добавляем LocationInstaller.
    // Логика регистрации — в LocationInstaller. См. docs/GameFlowLoop.md.
    public class LocationLifetimeScope : BaseLifetimeScope
    {
        protected override void InstallBindings(IContainerBuilder builder)
        {
        }
    }
}
