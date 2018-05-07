using System;


namespace PowerLanguage.Function
{
        public sealed class Dema : FunctionSeries<double>
        {
            public Dema(CStudyControl _master) : base(_master) {}
            public Dema(CStudyControl _master, int _ds) : base(_master, _ds) {}

            public ISeries<double> Price { private get; set; } 
            public int Length { private get; set; }
			
			private XAverage m_xaverage1;
	 		private XAverage m_xaverage2;

		    private VariableSeries<Double> m_body;
			private VariableSeries<Double> m_bodyavg;
			
			protected override void Create(){
		
				m_xaverage1 = new XAverage(this);
				m_xaverage2 = new XAverage(this);
				
				m_body = new VariableSeries<Double>(this);
				m_bodyavg = new VariableSeries<Double>(this);
				
			}
			protected override void StartCalc(){
            
				m_xaverage1.Price = Bars.Close;
	            m_xaverage1.Length = Length;
	            m_xaverage2.Price = m_body ;
	            m_xaverage2.Length = Length;
        	}
            protected override double CalcBar(){
             
				m_body.Value = m_xaverage1[0];
				
				if (1 == Bars.CurrentBar)
                    return Price[0];
				return 2 * m_xaverage1[0] - m_xaverage2[0];
            }
        }
}