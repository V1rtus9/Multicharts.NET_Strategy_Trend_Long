using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;

namespace PowerLanguage.Indicator{
	
	[SameAsSymbol(true)]
	public class Mov_Avg_Double : IndicatorObject {
		
		private Dema m_dema;
        private VariableSeries<Double> m_avgdouble;
		
		
		private IPlotObject    Plot1;
		private IPlotObjectStr Plot2;

        private ISeries<double> Price { get; set; }

        [Input]
        public int Length { get; set; }
        [Input]
        public int Displace { get; set; }
		[Input]
		public bool BreakSignal { get; set; }
		
		public Mov_Avg_Double(object _ctx):
			base(_ctx){
			Length = 30;
			BreakSignal = false;	
		}
		
		private bool RisingTrend  = true;
		private bool FallingTrend = false;
			
		enum MarketStages {
			Side,
			Rising,
			Falling
		}
		
		private string MarketStage = "NO_STAGE_DEFINED";
		private Color  CellColor = Color.AliceBlue;
		
		protected override void Create() {
			m_dema		 = new Dema(this);
            m_avgdouble  = new VariableSeries<Double>(this);
			//Plotting
			Plot1 =  AddPlot(new PlotAttributes("AvgDouble", 0, Color.Blue,Color.Empty, 0, 0, true));
			Plot2 =  AddPlot(new StringPlotAttributes("Trend", Color.Aqua));
		}
		
		protected override void StartCalc() {
			
			m_dema.Price = Bars.Close;
            m_dema.Length = Length;
			
		}
		protected override void CalcBar(){
			m_avgdouble.Value =  m_dema[0];
			// indicator logic 
			    if (((Displace >= 0) || Bars.CurrentBar > Math.Abs(Displace))){
					
               	 Plot1.Set(Displace, m_avgdouble.Value);
					
            	}
			if(BreakSignal){
			
				if(m_dema[0] >= Bars.Close[0]){
					
					if(!MarketStage.Equals("Falling")){
						MarketStage = "Falling";
						Alerts.Alert("Get ready for the falling trend start");
					}
					CellColor = Color.Red;
					#if DEBUG
						//Output.WriteLine("Be ready for break down");
					#endif
				}
				else if(m_dema[0] <= Bars.Close[0]){
					if(!MarketStage.Equals("Rising")){
						MarketStage = "Rising";	
						Alerts.Alert("Get ready for the rising trend start");
					}
					CellColor = Color.Green;
					#if DEBUG
						//Output.WriteLine("Be ready for break up");
					#endif
				}		
			}
			
			Plot2.Set(MarketStage, CellColor);
		}
	}
}