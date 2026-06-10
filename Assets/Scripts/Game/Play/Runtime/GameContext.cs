using System;
using System.Linq;
using System.Reflection;
using Game.Play.Base.Attributes;
using UniKit.Core.Utilities;
using UniKit.Framework.Base;
using UnityEngine;

namespace Game.Play
{
    public class GameContext : AbstractContext<GameContext>
    {
        protected override void OnInitialize()
        {
            InitModes();
            InitSystems();
        }

        private void InitModes()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var models = Utility.Reflections.GetImplementationsOf(assembly, typeof(AbstractModel));
            foreach (var type in models)
            {
                var model = (AbstractModel)Activator.CreateInstance(type);
                RegisterModel(model);
                Debug.Log($"[Initialize] Create Model: {type.Name}");
            }
        }

        private void InitSystems()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var types = Utility.Reflections.GetImplementationsOf(assembly, typeof(ISystem));
            var sortedTypes = types.OrderBy(t => t.GetCustomAttribute<OrderAttribute>()?.Order ?? int.MaxValue);

            foreach (var type in sortedTypes)
            {
                var system = (ISystem)Activator.CreateInstance(type);
                RegisterSystem(GetInterfaceType<ISystem>(type), system);
                Debug.Log($"[Initialize] Create System: {type.Name}");
            }
        }

        private static Type GetInterfaceType<T>(Type type)
        {
            var parent = typeof(T);
            var interfaces = type.GetInterfaces();
            
            foreach (var api in interfaces)
            {
                if (parent.IsAssignableFrom(api) && api != parent)
                {
                    return api;
                }
            }

            return interfaces.FirstOrDefault(api => parent.IsAssignableFrom(api) && api != parent);
        }
    }
}