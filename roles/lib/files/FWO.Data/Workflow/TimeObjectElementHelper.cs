namespace FWO.Data.Workflow
{
    /// <summary>Provides the canonical time-object element of a workflow task.</summary>
    public static class TimeObjectElementHelper
    {
        /// <summary>Finds the time element in a collection, if one exists.</summary>
        /// <param name="elements">The workflow elements to inspect.</param>
        /// <returns>The time element or <see langword="null"/>.</returns>
        public static WfReqElement? Find(IEnumerable<WfReqElement> elements)
        {
            return elements.FirstOrDefault(element => element.Field == ElemFieldType.time.ToString());
        }

        /// <summary>Returns the task's time element, creating one when necessary.</summary>
        /// <param name="task">The workflow task to update.</param>
        /// <returns>The existing or newly created time element.</returns>
        public static WfReqElement GetOrCreate(WfReqTask task)
        {
            WfReqElement? element = Find(task.Elements);
            if (element != null)
            {
                return element;
            }

            element = new WfReqElement
            {
                TaskId = task.Id,
                Field = ElemFieldType.time.ToString(),
                RequestAction = RequestAction.create.ToString()
            };
            task.Elements.Add(element);
            return element;
        }

        /// <summary>Gets the selector reference stored on a time element.</summary>
        /// <param name="element">The time element to inspect.</param>
        /// <returns>The Flow DB or ticket-local selector ID.</returns>
        public static long? GetSelectorId(WfReqElement? element)
        {
            if (element == null)
            {
                return null;
            }
            return element.FlowTimeObjectId ?? element.TimeObjId;
        }

        /// <summary>Stores a Flow DB or ticket-local selector reference.</summary>
        /// <param name="element">The time element to update.</param>
        /// <param name="selectorId">A non-zero positive or negative selector ID.</param>
        public static void SetReference(WfReqElement element, long selectorId)
        {
            if (selectorId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(selectorId), "A time-object reference must not be zero.");
            }

            element.FlowTimeObjectId = selectorId > 0 ? selectorId : null;
            // Negative IDs are UI selectors for ticket-local time elements.
            // They must not be written to time_obj_id because that column is
            // a foreign key to the persistent time_object table.
            element.TimeObjId = null;
            element.ObjectTaskId = null;
        }

        /// <summary>Stores a reference to the task defining the ticket-local time object.</summary>
        /// <param name="element">The time element to update.</param>
        /// <param name="taskId">The positive defining task ID.</param>
        public static void SetTicketTaskReference(WfReqElement element, long taskId)
        {
            if (taskId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(taskId), "A ticket time-object task reference must be positive.");
            }

            element.FlowTimeObjectId = null;
            element.TimeObjId = null;
            element.ObjectTaskId = taskId;
        }

        /// <summary>Clears all persisted time-object references from an element.</summary>
        /// <param name="element">The time element to update.</param>
        public static void ClearReference(WfReqElement element)
        {
            element.FlowTimeObjectId = null;
            element.TimeObjId = null;
            element.ObjectTaskId = null;
        }
    }
}
