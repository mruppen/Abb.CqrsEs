using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Abb.CqrsEs.Internal
{
    internal class CompiledMethodInfo
    {
        private readonly Func<object, object[], object?> _func;

        public Type[] ParameterTypes { get; private set; }

        public CompiledMethodInfo(MethodInfo methodInfo, Type type)
        {
            if (methodInfo == null)
            {
                throw ExceptionHelper.ArgumentMustNotBeNull(nameof(methodInfo));
            }

            if (type == null)
            {
                throw ExceptionHelper.ArgumentMustNotBeNull(nameof(type));
            }

            var instanceExpression = Expression.Parameter(typeof(object), "instance");
            var argumentsExpression = Expression.Parameter(typeof(object[]), "arguments");
            var parameterInfos = methodInfo.GetParameters();

            ParameterTypes = parameterInfos.Select(p => p.ParameterType).ToArray();

            var argumentExpressions = new Expression[parameterInfos.Length];
            for (var i = 0; i < parameterInfos.Length; ++i)
            {
                var parameterInfo = parameterInfos[i];
                argumentExpressions[i] = Expression.Convert(Expression.ArrayIndex(argumentsExpression, Expression.Constant(i)), parameterInfo.ParameterType);
            }
            var callExpression = Expression.Call(!methodInfo.IsStatic ? Expression.Convert(instanceExpression, type) : null, methodInfo, argumentExpressions);
            if (callExpression.Type == typeof(void))
            {
                var action = Expression.Lambda<Action<object, object[]>>(callExpression, instanceExpression, argumentsExpression).Compile();
                _func = (instance, arguments) =>
                {
                    action(instance, arguments);
                    return null;
                };
            }
            else
            {
                _func = Expression.Lambda<Func<object, object[], object>>(Expression.Convert(callExpression, typeof(object)), instanceExpression, argumentsExpression).Compile();
            }
        }

        public object? Invoke(object instance, params object[] arguments) => _func(instance, arguments);
    }
}
