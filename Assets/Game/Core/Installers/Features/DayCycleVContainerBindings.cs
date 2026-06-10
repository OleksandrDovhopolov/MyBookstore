using Game.DayCycle.Day;
using Game.DayCycle.Morning;
using Game.DayCycle.Morning.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Bootstrap
{
    // Main Feature (core loop). Registered in: GameInstaller (GameplayLifetimeScope).
    // Resolves from parent (GlobalLifetimeScope): ISaveService, IConfigsService.
    // Хостит общий day-state (IDayProgressService) и фазу «Утро».
    public static class DayCycleVContainerBindings
    {
        public static void RegisterDayCycle(this IContainerBuilder builder)
        {
            // --- Общий прогресс дня (день/фаза/золото/репутация) — основа всего core loop ---
            builder.Register<IDayProgressService, DayProgressService>(Lifetime.Singleton);

            // --- Фаза «Утро» ---
            builder.Register<IMorningContextResolver, MorningContextResolver>(Lifetime.Singleton);
            builder.Register<IMorningSessionService, MorningSessionService>(Lifetime.Singleton);

            // Debug-экран утра. Регистрируем только если он есть в сцене, чтобы проект
            // запускался и до ручной расстановки UI (uGUI-объект добавляется в GameplayScene).
            if (Object.FindAnyObjectByType<MorningScreenView>(FindObjectsInactive.Include) != null)
                builder.RegisterComponentInHierarchy<MorningScreenView>();
        }
    }
}
