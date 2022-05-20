namespace FWO.Ui.Services
{
    public class StateMatrix
    {
        private Dictionary<int, List<int>> matrix = new Dictionary<int, List<int>>();
        private Dictionary<int, int> derivedStates = new Dictionary<int, int>();
        public int LowestInputState { get; set; }
        public int LowestStartedState { get; set; }
        public int LowestEndState { get; set; }

        public void Init(int phase)
        {
            matrix = new Dictionary<int, List<int>>();
            derivedStates = new Dictionary<int, int>();
            switch(phase)
            {
                case 0:
                    matrix.Add(0, new List<int>(){0,49,620});
                    matrix.Add(49, new List<int>(){49,620});
                    matrix.Add(620, new List<int>(){620});
                    LowestInputState = 0;
                    LowestStartedState = 0;
                    LowestEndState = 49;
                    break;
                case 2:
                    matrix.Add(49, new List<int>(){110});
                    matrix.Add(110, new List<int>(){110,120,130,149});
                    matrix.Add(120, new List<int>(){120,110,130,149});
                    matrix.Add(130, new List<int>(){130,110,120,149,610});
                    matrix.Add(149, new List<int>(){149});
                    matrix.Add(610, new List<int>(){610});
                    LowestInputState = 49;
                    LowestStartedState = 110;
                    LowestEndState = 149;
                    derivedStates.Add(49, 49);
                    derivedStates.Add(110, 110);
                    derivedStates.Add(120, 110);
                    derivedStates.Add(130, 110);
                    derivedStates.Add(149, 149);
                    derivedStates.Add(610, 610);
                    break;
                default:
                    break;
            }
        }

        public List<int> getAllowedTransitions(int stateIn)
        {
            List<int> statesOut = new List<int>();
            if(matrix.ContainsKey(stateIn))
            {
                statesOut = matrix[stateIn];
            }
            return statesOut;
        }

        public int getRequestStateFromTaskStates(List<int> statesIn)
        {
            int stateOut = 0;
            int initState = 0;
            int inWorkState = LowestEndState;
            int maxState = 0;
            int openTasks = 0;
            int inWorkTasks = 0;
            int finishedTasks = 0;
            foreach(int state in statesIn)
            {
                if(state < LowestStartedState)
                {
                    openTasks++;
                    initState = state;
                }
                else if(state < LowestEndState)
                {
                    inWorkTasks++;
                    if(state < inWorkState)
                    {
                        inWorkState = state;
                    }
                }
                else
                {
                    finishedTasks++;
                    if(state > maxState)
                    {
                        maxState = state;
                    }
                }
            }
            if(inWorkTasks > 0)
            {
                stateOut = inWorkState;
            }
            else if(finishedTasks == statesIn.Count)
            {
                stateOut = maxState;
            }
            else if(openTasks == statesIn.Count)
            {
                stateOut = initState;
            }
            else
            {
                stateOut = LowestStartedState;
            }
            return derivedStates[stateOut];
        }
    }
}
