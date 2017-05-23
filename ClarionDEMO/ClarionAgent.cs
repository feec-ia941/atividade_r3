using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Clarion;
using Clarion.Framework;
using Clarion.Framework.Core;
using Clarion.Framework.Templates;
using WorldServerLibrary.Model;
using WorldServerLibrary;
using System.Threading;
using Gtk;

namespace ClarionDEMO
{
    /// <summary>
    /// Public enum that represents all possibilities of agent actions
    /// </summary>
    public enum CreatureActions
    {
        DO_NOTHING,
        ROTATE_CLOCKWISE,
        GO_ITEM,
		SACKIT_ITEM,
		EAT_ITEM,
		STOP_CREATURE
    }

    public class ClarionAgent
    {
        #region Constants
        /// <summary>
        /// Constant that represents the Visual Sensor
        /// </summary>
        private String SENSOR_VISUAL_DIMENSION = "VisualSensor";
        /// <summary>
        /// Constant that represents that there is at least one wall ahead
        /// </summary>
        private String DIMENSION_AHEAD = "Ahead";

		private String DIMENSION_GO_ITEM = "GoItem";

		private String DIMENSION_SACKIT_ITEM = "SackItItem";

		private String DIMENSION_EAT_ITEM = "EatItem";

		private String DIMENSION_STOP_CREATURE = "StopCreature";


		double prad = 0;
        #endregion

        #region Properties
		public Mind mind;
		String creatureId = String.Empty;
		String creatureName = String.Empty;
		Thing PreferenceThing = null;
        #region Simulation
        /// <summary>
        /// If this value is greater than zero, the agent will have a finite number of cognitive cycle. Otherwise, it will have infinite cycles.
        /// </summary>
        public double MaxNumberOfCognitiveCycles = -1;
        /// <summary>
        /// Current cognitive cycle number
        /// </summary>
        private double CurrentCognitiveCycle = 0;
        /// <summary>
        /// Time between cognitive cycle in miliseconds
        /// </summary>
        public Int32 TimeBetweenCognitiveCycles = 0;
        /// <summary>
        /// A thread Class that will handle the simulation process
        /// </summary>
        private Thread runThread;
        #endregion

        #region Agent
		private WorldServer worldServer;
        /// <summary>
        /// The agent 
        /// </summary>
        private Clarion.Framework.Agent CurrentAgent;
        #endregion

        #region Perception Input
        /// <summary>
        /// Perception input to indicates a wall ahead
        /// </summary>
		private DimensionValuePair inputAhead;

		private DimensionValuePair inputGoItem;

		private DimensionValuePair inputSackItItem;

		private DimensionValuePair inputEatItem;

		private DimensionValuePair inputStopCreature;


        #endregion

        #region Action Output
        /// <summary>
        /// Output action that makes the agent to rotate clockwise
        /// </summary>
		private ExternalActionChunk outputRotateClockwise;

		private ExternalActionChunk outputGoItem;

		private ExternalActionChunk outputSackItItem;

		private ExternalActionChunk outputEatItem;

		private ExternalActionChunk outputStopCreature;
        /// <summary>
        /// Output action that makes the agent go ahead
        /// </summary>

        #endregion

        #endregion

        #region Constructor
		public ClarionAgent(WorldServer nws, String creature_ID, String creature_Name)
        {
			worldServer = nws;
			// Initialize the agent
            CurrentAgent = World.NewAgent("Current Agent");
			mind = new Mind();
			mind.Show ();
			creatureId = creature_ID;
			creatureName = creature_Name;

            // Initialize Input Information
            inputAhead = World.NewDimensionValuePair(SENSOR_VISUAL_DIMENSION, DIMENSION_AHEAD);

			inputGoItem= World.NewDimensionValuePair(SENSOR_VISUAL_DIMENSION, DIMENSION_GO_ITEM);

			inputSackItItem= World.NewDimensionValuePair(SENSOR_VISUAL_DIMENSION, DIMENSION_SACKIT_ITEM);

			inputEatItem= World.NewDimensionValuePair(SENSOR_VISUAL_DIMENSION, DIMENSION_EAT_ITEM);

			inputStopCreature= World.NewDimensionValuePair(SENSOR_VISUAL_DIMENSION, DIMENSION_STOP_CREATURE);

            // Initialize Output actions
            outputRotateClockwise = World.NewExternalActionChunk(CreatureActions.ROTATE_CLOCKWISE.ToString());

			outputGoItem = World.NewExternalActionChunk(CreatureActions.GO_ITEM.ToString());

			outputSackItItem = World.NewExternalActionChunk(CreatureActions.SACKIT_ITEM.ToString());

			outputEatItem = World.NewExternalActionChunk(CreatureActions.EAT_ITEM.ToString());

			outputStopCreature = World.NewExternalActionChunk(CreatureActions.STOP_CREATURE.ToString());
            

            //Create thread to simulation
            runThread = new Thread(CognitiveCycle);
			Console.WriteLine("Agent started");
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Run the Simulation in World Server 3d Environment
        /// </summary>
        public void Run()
        {                
			Console.WriteLine ("Running ...");
            // Setup Agent to run
            if (runThread != null && !runThread.IsAlive)
            {
                SetupAgentInfraStructure();
				// Start Simulation Thread                
                runThread.Start(null);
            }
        }

        /// <summary>
        /// Abort the current Simulation
        /// </summary>
        /// <param name="deleteAgent">If true beyond abort the current simulation it will die the agent.</param>
        public void Abort(Boolean deleteAgent)
        {   Console.WriteLine ("Aborting ...");
            if (runThread != null && runThread.IsAlive)
            {
                runThread.Abort();
            }

            if (CurrentAgent != null && deleteAgent)
            {
                CurrentAgent.Die();
            }
        }

		IList<Thing> processSensoryInformation()
		{
			IList<Thing> response = null;

			if (worldServer != null && worldServer.IsConnected)
			{
				response = worldServer.SendGetCreatureState(creatureName);

				if (response != null) {
					prad = (Math.PI / 180) * response.First ().Pitch;
					while (prad > Math.PI)
						prad -= 2 * Math.PI;
					while (prad < -Math.PI)
						prad += 2 * Math.PI;
					Sack s = worldServer.SendGetSack ("0");
					mind.setBag (s);
				}
			}

			return response;
		}

		void processSelectedAction(CreatureActions externalAction)
		{   Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
			if (worldServer != null && worldServer.IsConnected)
			{
				switch (externalAction)
				{
				case CreatureActions.DO_NOTHING:
					// Do nothing as the own value says
					break;
				case CreatureActions.GO_ITEM:
					if(PreferenceThing != null)						
						worldServer.SendSetGoTo (creatureId, 1, 1, PreferenceThing.comX, PreferenceThing.comY);
					break;
				case CreatureActions.SACKIT_ITEM:
					if (PreferenceThing != null)
						worldServer.SendSackIt (creatureId, PreferenceThing.Name);
					break;
				case CreatureActions.EAT_ITEM:
					if (PreferenceThing != null)
						worldServer.SendEatIt (creatureId, PreferenceThing.Name);					
					break;
				case CreatureActions.STOP_CREATURE:					
					worldServer.SendStopCreature (creatureId);
					break;
				case CreatureActions.ROTATE_CLOCKWISE:
					worldServer.SendSetAngle(creatureId, 2, -2, 1);
					break;				
				default:
					break;
				}
			}
		}

        #endregion

        #region Setup Agent Methods
        /// <summary>
        /// Setup agent infra structure (ACS, NACS, MS and MCS)
        /// </summary>
        private void SetupAgentInfraStructure()
        {
            // Setup the ACS Subsystem
            SetupACS();                    
        }

        private void SetupMS()
        {            
            //RichDrive
        }

        /// <summary>
        /// Setup the ACS subsystem
        /// </summary>
        private void SetupACS()
        {
            // Create Rule to avoid collision with wall
            SupportCalculator avoidCollisionWallSupportCalculator = FixedRuleToAvoidCollisionWall;
            FixedRule ruleAvoidCollisionWall = AgentInitializer.InitializeActionRule(CurrentAgent, FixedRule.Factory, outputRotateClockwise, avoidCollisionWallSupportCalculator);

            // Commit this rule to Agent (in the ACS)
            CurrentAgent.Commit(ruleAvoidCollisionWall);


			// Create Rule to avoid collision with wall
			SupportCalculator goItemSupportCalculator = FixedRuleToGoItem;
			FixedRule ruleGoItem = AgentInitializer.InitializeActionRule(CurrentAgent, FixedRule.Factory, outputGoItem, goItemSupportCalculator);

			// Commit this rule to Agent (in the ACS)
			CurrentAgent.Commit(ruleGoItem);



			// Create Rule to avoid collision with wall
			SupportCalculator sackItItemSupportCalculator = FixedRuleToSackItItem;
			FixedRule ruleSackItItem = AgentInitializer.InitializeActionRule(CurrentAgent, FixedRule.Factory, outputSackItItem, sackItItemSupportCalculator);

			// Commit this rule to Agent (in the ACS)
			CurrentAgent.Commit(ruleSackItItem);


			// Create Rule to avoid collision with wall
			SupportCalculator eatItemSupportCalculator = FixedRuleToEatItem;
			FixedRule ruleEatItem = AgentInitializer.InitializeActionRule(CurrentAgent, FixedRule.Factory, outputEatItem, eatItemSupportCalculator);

			// Commit this rule to Agent (in the ACS)
			CurrentAgent.Commit(ruleEatItem);


			// Create Rule to avoid collision with wall
			SupportCalculator stopCreatureSupportCalculator = FixedRuleToStopCreature;
			FixedRule ruleStopCreature = AgentInitializer.InitializeActionRule(CurrentAgent, FixedRule.Factory, outputStopCreature, stopCreatureSupportCalculator);

			// Commit this rule to Agent (in the ACS)
			CurrentAgent.Commit(ruleStopCreature);


            

            // Disable Rule Refinement
            CurrentAgent.ACS.Parameters.PERFORM_RER_REFINEMENT = false;

            // The selection type will be probabilistic
            CurrentAgent.ACS.Parameters.LEVEL_SELECTION_METHOD = ActionCenteredSubsystem.LevelSelectionMethods.STOCHASTIC;

            // The action selection will be fixed (not variable) i.e. only the statement defined above.
            CurrentAgent.ACS.Parameters.LEVEL_SELECTION_OPTION = ActionCenteredSubsystem.LevelSelectionOptions.FIXED;

            // Define Probabilistic values
            CurrentAgent.ACS.Parameters.FIXED_FR_LEVEL_SELECTION_MEASURE = 1;
            CurrentAgent.ACS.Parameters.FIXED_IRL_LEVEL_SELECTION_MEASURE = 0;
            CurrentAgent.ACS.Parameters.FIXED_BL_LEVEL_SELECTION_MEASURE = 0;
            CurrentAgent.ACS.Parameters.FIXED_RER_LEVEL_SELECTION_MEASURE = 0;
        }

        /// <summary>
        /// Make the agent perception. In other words, translate the information that came from sensors to a new type that the agent can understand
        /// </summary>
        /// <param name="sensorialInformation">The information that came from server</param>
        /// <returns>The perceived information</returns>
		private SensoryInformation prepareSensoryInformation(IList<Thing> listOfThings)
        {

			Creature c = (Creature) listOfThings.Where(item => (item.CategoryId == Thing.CATEGORY_CREATURE)).First();


			PreferenceThing = null;
			Boolean goItem = false;
			Boolean Ahead = false;
			Boolean SackItItem = false;
			Boolean EatItem = false;
			Boolean wallAhead = false;
			Boolean situationLeaflets = false;

            // New sensory information
            SensoryInformation si = World.NewSensoryInformation(CurrentAgent);

            // Detect if we have a wall ahead



            wallAhead = listOfThings.Where(item => (item.CategoryId == Thing.CATEGORY_BRICK && item.DistanceToCreature <= 70)).Any();

			var listLeaflets = c.getLeaflets ();



			

			// Detect if we have itens 
			List<Thing> listItem = listOfThings
				.Where (item => (item.CategoryId != Thing.CATEGORY_CREATURE && item.CategoryId != Thing.CATEGORY_BRICK && item.DistanceToCreature <= 500))
				.OrderBy(x=> x.DistanceToCreature)
				.ToList ();

			if (!c.HasCollided) {

				if (listItem.Count () > 0 && PreferenceThing == null && wallAhead != true) {
					//If detect wall eat with mor distance
					int DISTANCE_SACK = wallAhead == true ? 70 : 40;

					PreferenceThing = listItem.FirstOrDefault ();

					if (PreferenceThing.DistanceToCreature < DISTANCE_SACK) {
						if (PreferenceThing.CategoryId == Thing.CATEGORY_JEWEL)
							SackItItem = true;
						else
							EatItem = true;
					} else {

						Thing preferenceJewel = PreferenceThing;

						int countSituation = 0;

						foreach (var leaflets in listLeaflets) {
							foreach (var item in leaflets.items) {
								if (item.collected < item.totalNumber) {
									Thing thing = listItem.Where (x => x.CategoryId == Thing.CATEGORY_JEWEL).FirstOrDefault();
									if(thing!=null)										
										if (thing.Material.Color == item.itemKey)
										preferenceJewel = thing;
								}
							}

							Console.WriteLine (leaflets.leafletID+" - Pay:"+leaflets.payment+" Situation:"+leaflets.situation);

							situationLeaflets = leaflets.situation;

							if (situationLeaflets)
								countSituation++;
						}

						situationLeaflets = countSituation == 3 ? true : false;

						Console.WriteLine("situationLeaflets: "+situationLeaflets);

						if (!situationLeaflets) {

							if (PreferenceThing.DistanceToCreature >= preferenceJewel.DistanceToCreature) {
								PreferenceThing = preferenceJewel;
							}


							if (c.Fuel > 400 && PreferenceThing.CategoryId == Thing.CATEGORY_JEWEL)
								goItem = true;
							else if (PreferenceThing.CategoryId != Thing.CATEGORY_JEWEL && PreferenceThing.DistanceToCreature < 170)
								goItem = true;
							else
								Ahead = true;
						}
							
						
					}
				
				} else {
					Ahead = true;
				}
			} else {				

				PreferenceThing = listItem.OrderBy (x => x.DistanceToCreature).FirstOrDefault ();
				if (PreferenceThing != null) {					
					if (PreferenceThing.CategoryId == Thing.CATEGORY_JEWEL)
						SackItItem = true;
					else
						EatItem = true;
				}
			}



			double wallAheadActivationValue = Ahead ? CurrentAgent.Parameters.MAX_ACTIVATION : CurrentAgent.Parameters.MIN_ACTIVATION;

			double goItemActivationValue = goItem ? CurrentAgent.Parameters.MAX_ACTIVATION : CurrentAgent.Parameters.MIN_ACTIVATION;

			double sackItItemActivationValue = SackItItem ? CurrentAgent.Parameters.MAX_ACTIVATION : CurrentAgent.Parameters.MIN_ACTIVATION;

			double eatItemActivationValue = EatItem ? CurrentAgent.Parameters.MAX_ACTIVATION : CurrentAgent.Parameters.MIN_ACTIVATION;

			double stopCreatureActivationValue = situationLeaflets ? CurrentAgent.Parameters.MAX_ACTIVATION : CurrentAgent.Parameters.MIN_ACTIVATION;


            si.Add(inputAhead, wallAheadActivationValue);

			si.Add(inputGoItem, goItemActivationValue);

			si.Add(inputSackItItem, sackItItemActivationValue);

			si.Add(inputEatItem, eatItemActivationValue);

			si.Add(inputStopCreature, stopCreatureActivationValue);

			//Console.WriteLine(sensorialInformation);

			int n = 0;
			foreach(Leaflet l in c.getLeaflets()) {
				mind.updateLeaflet(n,l);
				n++;
			}
            return si;
        }
        #endregion

        #region Fixed Rules
        private double FixedRuleToAvoidCollisionWall(ActivationCollection currentInput, Rule target)
        {
            // See partial match threshold to verify what are the rules available for action selection
            return ((currentInput.Contains(inputAhead, CurrentAgent.Parameters.MAX_ACTIVATION))) ? 1.0 : 0.0;
        }

		private double FixedRuleToGoItem(ActivationCollection currentInput, Rule target)
		{
			// See partial match threshold to verify what are the rules available for action selection
			return ((currentInput.Contains(inputGoItem, CurrentAgent.Parameters.MAX_ACTIVATION))) ? 1.0 : 0.0;
		}

		private double FixedRuleToSackItItem(ActivationCollection currentInput, Rule target)
		{
			// See partial match threshold to verify what are the rules available for action selection
			return ((currentInput.Contains(inputSackItItem, CurrentAgent.Parameters.MAX_ACTIVATION))) ? 1.0 : 0.0;
		}

		private double FixedRuleToEatItem(ActivationCollection currentInput, Rule target)
		{
			// See partial match threshold to verify what are the rules available for action selection
			return ((currentInput.Contains(inputEatItem, CurrentAgent.Parameters.MAX_ACTIVATION))) ? 1.0 : 0.0;
		}

		private double FixedRuleToStopCreature(ActivationCollection currentInput, Rule target)
		{
			// See partial match threshold to verify what are the rules available for action selection
			return ((currentInput.Contains(inputStopCreature, CurrentAgent.Parameters.MAX_ACTIVATION))) ? 1.0 : 0.0;
		}










        
        #endregion

        #region Run Thread Method
        private void CognitiveCycle(object obj)
        {

			Console.WriteLine("Starting Cognitive Cycle ... press CTRL-C to finish !");
            // Cognitive Cycle starts here getting sensorial information
            while (CurrentCognitiveCycle != MaxNumberOfCognitiveCycles)
            {   
				// Get current sensory information                    
				IList<Thing> currentSceneInWS3D = processSensoryInformation();

				if (currentSceneInWS3D != null) {
					// Make the perception
					SensoryInformation si = prepareSensoryInformation (currentSceneInWS3D);

					//Perceive the sensory information
					CurrentAgent.Perceive (si);

					//Choose an action
					ExternalActionChunk chosen = CurrentAgent.GetChosenExternalAction (si);

					// Get the selected action
					String actionLabel = chosen.LabelAsIComparable.ToString ();
					CreatureActions actionType = (CreatureActions)Enum.Parse (typeof(CreatureActions), actionLabel, true);

					// Call the output event handler
					processSelectedAction (actionType);

					// Increment the number of cognitive cycles
					CurrentCognitiveCycle++;

					//Wait to the agent accomplish his job
					if (TimeBetweenCognitiveCycles > 0) {
						Thread.Sleep (TimeBetweenCognitiveCycles);
					}
				}
			}
        }
        #endregion

    }
}
