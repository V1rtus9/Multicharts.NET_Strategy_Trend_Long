using System;
using System.Drawing;
using System.Linq;
using PowerLanguage.Function;
using ATCenterProxy.interop;
using System.Collections.Generic;

namespace PowerLanguage.Strategy {

	public delegate void EMarketPositionChangeDelegate();

	public class Dema_Break_W_Long : SignalObject {

		private IOrderPriced FE; // first enter
		private IOrderPriced SL1;// stoploss for first enter (FE)
		private IOrderPriced TralingSL1;// trailing stoploss for first enter (FE)

		private ISeries<double> DailyPrices { get; set; }
		private ISeries<double> WeeklyPrices { get; set; }

		private Dema DailyDema;
		private Dema WeeklyDema;

		[Input]
		public int DailyDemaLenght { get; set; }   // moving average lenght
		[Input]
		public int WeeklyDemaLenght { get; set; }   // moving average lenght
		[Input]
		public double FirstEnterRate { get; set; }      //  percent for break price calculation
		[Input]
		public double SL1Rate { get; set; }     //  percent for initial stoploss calculation ( weekly )
		[Input]
		public int MaxNewPositionRiskUSD { get; set; } // max risk for new position
		[Input]
		public int TrailingSLCalcBarStart { get; set; } // How many bars after position open we start with trailing stop
		[Input]
		public double TrailingSL1WeeklyRate { get; set; } 
		[Input]
		public DateTime StrategyCalcStartDate { get; set; }

		public Dema_Break_W_Long(object _ctx) : base(_ctx) {
			//Define every input basic value

			DailyDemaLenght = 30; // Double moving average indicator (daily bars)
			WeeklyDemaLenght = 30; // Double moving average indicator (weekly bars)
			FirstEnterRate = 1.1; // in percents
			SL1Rate = 1.6; // in perecents
			MaxNewPositionRiskUSD = 400; // Maximum risk in USD for position, based on this number will be calculated new position lot size

			TrailingSLCalcBarStart = 15; // how mnay bars need to be wait before strategy will start using trailing stop
			TrailingSL1WeeklyRate = 1.6; // in percents

			StrategyCalcStartDate = new DateTime(2016, 1, 4, new System.Globalization.GregorianCalendar()); // Date when strategy will start calculation (without this parametr it will start calc from first bar)
		}

		private double FirstEnterDynamicValue;
		private double StopLossDynamicValue;

		private int InitialContractSizeDynamicValue;

		private event EMarketPositionChangeDelegate EMarketPositionOpened;
		private event EMarketPositionChangeDelegate EMarketPositionClosed;

		private Dictionary<EMarketPositionSide, Action<double, double>> StrategyPositionStates;

		protected override void Create() {
			// create variable objects, function objects, order objects etc.
			DailyDema = new Dema(this, 1);
			WeeklyDema = new Dema(this, 2);

			FE  = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "FE", EOrderAction.Buy));
			SL1 = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "SL1", EOrderAction.Sell, OrderExit.FromEntry(FE)));
			TralingSL1 = OrderCreator.Stop(new SOrderParameters(Contracts.UserSpecified, "Trail1", EOrderAction.Sell, OrderExit.FromEntry(FE)));

			StrategyPositionStates = new Dictionary<EMarketPositionSide, Action<double, double>>()
			{
				{EMarketPositionSide.Flat, this.EMarketPositionSideFlat},
				{EMarketPositionSide.Long, this.EMarketPositionSideLong},
				{EMarketPositionSide.Short, this.EMarketPositionSideShort}
			};
		}
		protected override void StartCalc() {
			// assign inputs 

			DailyPrices  = Bars.Close;
			WeeklyPrices = BarsOfData(2).Close;

			DailyDema.Price   = DailyPrices;
			DailyDema.Length  = DailyDemaLenght;
			WeeklyDema.Price  = WeeklyDema;
			WeeklyDema.Length = WeeklyDemaLenght;

			EMarketPositionOpened += EMarket_PositionOpened;
			EMarketPositionClosed += EMarket_PositionClosed;

		}

		protected override void CalcBar() {
			// strategy logic 

			if (Bars.Time.Value.CompareTo(StrategyCalcStartDate) < 0)
				return;

			double CurrentBarDDema = Math.Round(DailyDema[0], 2, MidpointRounding.ToEven);
			double CurrentBarWDema = Math.Round(WeeklyDema[0], 2, MidpointRounding.ToEven);

			StrategyPositionStates[CurrentPosition.Side].Invoke(CurrentBarDDema, CurrentBarWDema);

			PositionsChangeEvents();
		}

		protected void EMarketPositionSideFlat(double _ddema, double _wdema)
		{
			//Output.WriteLine("EMarketPositionSideFlat");
			
			if (BarsOfData(2).High[0] > _wdema)
				return;

			FirstEnterDynamicValue = _wdema + (_wdema * (FirstEnterRate / 100)); // first enter price calculation
			StopLossDynamicValue   = _wdema - (_wdema * (SL1Rate / 100));    // initial stop price calcualtion	

			InitialContractSizeDynamicValue = CalculateInitialContractSize(FirstEnterDynamicValue, StopLossDynamicValue);
			
			//Output.WriteLine("Enter price: " + FirstEnterDynamicValue + "Stoploss: " + StopLossDynamicValue);
			
			FE.Send(FirstEnterDynamicValue, InitialContractSizeDynamicValue); // Place enter stop order
			SL1.Send(StopLossDynamicValue, InitialContractSizeDynamicValue); //Place stoploss order
		}

		protected void EMarketPositionSideLong(double _ddema, double _wdema)
		{
			//Output.WriteLine("EMarketPositionSideLong");
			ManageStrategyStoploss(_wdema);
		}

		protected void EMarketPositionSideShort(double _ddema, double _wdema)
		{
			//Output.WriteLine("EMarketPositionSideShort");
		}

		protected int CalculateInitialContractSize(double enter, double stop)
		{
			double RiskPerOneLot = (enter - stop) * 100;

			if (RiskPerOneLot < MaxNewPositionRiskUSD)
			{
				return (int)(MaxNewPositionRiskUSD / RiskPerOneLot) * 100;
			}

				return 100; // 1 lot

		}

		private int DailyBarCounter;
		protected void ManageStrategyStoploss(double _wdema)
		{
			DailyBarCounter++;

			if (DailyBarCounter < TrailingSLCalcBarStart)
			{
				SL1.Send(StopLossDynamicValue, InitialContractSizeDynamicValue); //Place basic stoploss order
				return;
			}

			double TrailingStopLoss = _wdema - (_wdema * (TrailingSL1WeeklyRate / 100));

			TralingSL1.Send(TrailingStopLoss, InitialContractSizeDynamicValue); //Place trailing stoploss order
		}


		private int PreviousOpenPositionsCount;
		protected void PositionsChangeEvents()
		{
			int CurrentOpenPositionsCount = CurrentPosition.OpenTrades.Count;

			if(CurrentOpenPositionsCount != PreviousOpenPositionsCount)
			{
				if (CurrentOpenPositionsCount > 0)
				{
					EMarketPositionOpened();// invoke event
					PreviousOpenPositionsCount = CurrentOpenPositionsCount;
					return;
				}

				EMarketPositionClosed(); // invoke event
				PreviousOpenPositionsCount = CurrentOpenPositionsCount;
			}


		}

		protected void EMarket_PositionOpened()
		{
			//nothing
		}

		protected void EMarket_PositionClosed()
		{
			DailyBarCounter = 0;
		}
	}
}