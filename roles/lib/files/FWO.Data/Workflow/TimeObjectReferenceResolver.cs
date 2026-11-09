namespace FWO.Data.Workflow
{
    /// <summary>Resolves the workflow representation of a ticket time object.</summary>
    public static class TimeObjectReferenceResolver
    {
        /// <summary>Returns local time elements from object-create tasks.</summary>
        public static IEnumerable<WfReqElement> GetTicketTimeElements(IEnumerable<WfReqTask> tasks)
        {
            return tasks
                .Where(task => task.TaskType == WfTaskType.object_create.ToString())
                .SelectMany(task => task.Elements)
                .Where(element => element.Field == ElemFieldType.time.ToString())
                .Where(element => element.FlowTimeObjectId == null && (element.TimeObjId == null || element.TimeObjId < 0));
        }

        /// <summary>Returns the selector ID, using a stable synthetic ID for unsaved elements.</summary>
        public static long GetSelectorId(WfReqElement element)
        {
            return element.TimeObjId ?? -Math.Abs(element.Id);
        }

        /// <summary>Finds the linked parallel time element without guessing between ambiguous dates.</summary>
        public static WfReqElement? FindParallelTimeElement(IEnumerable<WfReqTask> tasks, WfReqTask accessTask,
            long? timeObjectId, long? ticketElementId)
        {
            List<WfReqElement> timeElements = GetTicketTimeElements(tasks).ToList();

            if (timeObjectId.HasValue)
            {
                WfReqElement? byObjectId = timeElements.FirstOrDefault(element =>
                    element.TimeObjId == timeObjectId || element.FlowTimeObjectId == timeObjectId);
                if (byObjectId != null)
                {
                    return byObjectId;
                }
            }

            if (ticketElementId.HasValue)
            {
                WfReqElement? byElementId = timeElements.FirstOrDefault(element => element.Id == ticketElementId);
                if (byElementId != null)
                {
                    return byElementId;
                }
            }

            long? referencedTaskId = TimeObjectElementHelper.Find(accessTask.Elements)?.ObjectTaskId;
            if (referencedTaskId is > 0)
            {
                WfReqTask? referencedTask = tasks.FirstOrDefault(task => task.Id == referencedTaskId.Value);
                WfReqElement? referencedElement = referencedTask?.Elements.FirstOrDefault(element =>
                    element.Field == ElemFieldType.time.ToString());
                if (referencedElement != null)
                {
                    return referencedElement;
                }
            }

            List<WfReqElement> matchingDates = timeElements.Where(element =>
                element.StartTime == accessTask.TargetBeginDate
                && element.EndTime == accessTask.TargetEndDate).ToList();
            if (matchingDates.Count == 1)
            {
                return matchingDates[0];
            }

            string? accessTimeObjectName = TimeObjectElementHelper.Find(accessTask.Elements)?.Name;
            List<WfReqElement> matchingNames = timeElements.Where(element =>
                !string.IsNullOrWhiteSpace(accessTimeObjectName)
                && string.Equals(element.Name, accessTimeObjectName, StringComparison.OrdinalIgnoreCase)).ToList();
            return matchingNames.Count == 1 ? matchingNames[0] : null;
        }

        /// <summary>Finds the task defining the time object referenced by an access task.</summary>
        /// <param name="tasks">All tasks in the ticket.</param>
        /// <param name="accessTask">The access task whose time reference is resolved.</param>
        /// <param name="timeObjectId">An optional stored object or selector ID.</param>
        /// <param name="ticketElementId">An optional ticket-local element ID.</param>
        /// <returns>The defining task or <see langword="null"/>.</returns>
        public static WfReqTask? FindParallelTimeTask(IEnumerable<WfReqTask> tasks, WfReqTask accessTask,
            long? timeObjectId, long? ticketElementId)
        {
            List<WfReqTask> taskList = tasks.ToList();
            WfReqElement? element = FindParallelTimeElement(taskList, accessTask, timeObjectId, ticketElementId);
            return element == null
                ? null
                : taskList.FirstOrDefault(task => task.Elements.Contains(element));
        }

    }
}
