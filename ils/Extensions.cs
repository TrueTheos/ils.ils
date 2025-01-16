using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ils.IRGenerator;

namespace ils
{
    public static class Extensions
    {
        public static T DequeueItemWithCondition<T>(this Queue<T> queue, Func<T, bool> condition)
        {
            if (queue == null)
                throw new ArgumentNullException(nameof(queue));
            if (condition == null)
                throw new ArgumentNullException(nameof(condition));

            int queueCount = queue.Count;
            for (int i = 0; i < queueCount; i++)
            {
                T currentItem = queue.Dequeue();
                if (condition(currentItem))
                {
                    // If the item matches the condition, return it
                    return currentItem;
                }
                else
                {
                    // If the item doesn't match the condition, enqueue it back
                    queue.Enqueue(currentItem);
                }
            }
            // If no item matches the condition, return the default value for the type
            return default(T);
        }

        public static VarID ToVarID(this string id) => new VarID(id);
    }
}
