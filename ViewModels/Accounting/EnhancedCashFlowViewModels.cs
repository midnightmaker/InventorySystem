using System.ComponentModel.DataAnnotations;

namespace InventorySystem.ViewModels.Accounting
{
    public class EnhancedCashFlowAnalysisViewModel
    {
        public CashFlowStatementViewModel CurrentPeriod { get; set; } = new();
        public CashFlowStatementViewModel? PriorPeriod { get; set; }
        
        // Comparative Analysis
        public decimal OperatingCashFlowChange => CurrentPeriod.NetCashFromOperations - (PriorPeriod?.NetCashFromOperations ?? 0);
        public decimal OperatingCashFlowChangePercent => PriorPeriod?.NetCashFromOperations != 0 
            ? (OperatingCashFlowChange / Math.Abs(PriorPeriod.NetCashFromOperations)) * 100 : 0;
        
        // Working Capital Analysis
        public WorkingCapitalAnalysisViewModel WorkingCapitalAnalysis { get; set; } = new();
        
        // Free Cash Flow
        public FreeCashFlowAnalysisViewModel FreeCashFlow { get; set; } = new();
        
        // Cash Efficiency Metrics
        public decimal CashConversionCycle { get; set; }
        public decimal DaysInAccountsReceivable { get; set; }
        public decimal DaysInAccountsPayable { get; set; }
        public decimal DaysInInventory { get; set; }
        
        // Quality of Earnings
        public decimal QualityOfEarningsRatio => CurrentPeriod.NetIncome != 0 
            ? CurrentPeriod.NetCashFromOperations / CurrentPeriod.NetIncome : 0;
        
        // Cash Flow Ratios
        public List<CashFlowRatio> CashFlowRatios { get; set; } = new();
        
        // Trend Analysis
        public List<MonthlyCashFlowTrend> MonthlyTrends { get; set; } = new();
    }
    
    public class WorkingCapitalAnalysisViewModel
    {
        public decimal CurrentWorkingCapital { get; set; }
        public decimal PriorWorkingCapital { get; set; }
        public decimal WorkingCapitalChange => CurrentWorkingCapital - PriorWorkingCapital;
        
        // Working Capital Components
        public decimal AccountsReceivableChange { get; set; }
        public decimal InventoryChange { get; set; }
        public decimal AccountsPayableChange { get; set; }
        public decimal AccruedLiabilitiesChange { get; set; }
        
        // Working Capital Efficiency
        public decimal WorkingCapitalTurnover { get; set; }
        public decimal WorkingCapitalToSalesRatio { get; set; }
        
        public List<WorkingCapitalComponent> Components { get; set; } = new();
    }
    
    public class FreeCashFlowAnalysisViewModel
    {
        public decimal OperatingCashFlow { get; set; }
        
        // ✅ FIXED: Changed from CapitalExpenditures to TotalCapitalExpenditures (decimal)
        public decimal TotalCapitalExpenditures { get; set; }
        
        // ✅ FIXED: Free Cash Flow calculation using the renamed property
        public decimal FreeCashFlow => OperatingCashFlow - TotalCapitalExpenditures;
        
        // Free Cash Flow Metrics
        public decimal FreeCashFlowYield { get; set; }
        public decimal FreeCashFlowMargin { get; set; }
        public decimal FreeCashFlowToEquity { get; set; }
        
        // Cash Flow Adequacy
        public decimal CashFlowAdequacyRatio { get; set; }
        public decimal DividendPayoutFromCashFlow { get; set; }
        
        // ✅ FIXED: Detailed capital expenditures list with proper naming
        public List<CapitalExpenditureDetail> CapitalExpenditureDetails { get; set; } = new();
    }
    
    public class CustomerCashFlowAnalysisViewModel
    {
        public List<CustomerCashFlow> CustomerCashFlows { get; set; } = new();
        public decimal TotalCollections { get; set; }
        public decimal AverageCollectionPeriod { get; set; }
        public decimal CollectionEfficiency { get; set; }
        
        // Top customers by cash contribution
        public List<CustomerCashFlow> TopCashCustomers => 
            CustomerCashFlows.OrderByDescending(c => c.NetCashFlow).Take(10).ToList();
        
        // Collection trends
        public List<MonthlyCollectionTrend> CollectionTrends { get; set; } = new();
    }
    
    public class CashFlowProjectionViewModel
    {
        public List<MonthlyProjection> Projections { get; set; } = new();
        public decimal ProjectedCashShortfall { get; set; }
        public DateTime? ProjectedCashShortfallDate { get; set; }
        public decimal MinimumCashRequired { get; set; }
        
        // Scenario Analysis
        public ProjectionScenario OptimisticScenario { get; set; } = new();
        public ProjectionScenario PessimisticScenario { get; set; } = new();
        public ProjectionScenario MostLikelyScenario { get; set; } = new();
    }
    
    // Supporting Classes
    public class CashFlowRatio
    {
        public string RatioName { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public string FormattedValue { get; set; } = string.Empty;
        public string Interpretation { get; set; } = string.Empty;
        public RatioBenchmark Benchmark { get; set; }
    }
    
    public class MonthlyCashFlowTrend
    {
        public DateTime Month { get; set; }
        public decimal OperatingCashFlow { get; set; }
        public decimal InvestingCashFlow { get; set; }
        public decimal FinancingCashFlow { get; set; }
        public decimal NetCashFlow { get; set; }
        public decimal EndingCashBalance { get; set; }
        public string MonthName => Month.ToString("MMM yyyy");
        
        // ✅ ADD: Additional trend analysis properties
        public decimal CashFlowGrowthRate { get; set; }
        public bool IsPositiveCashFlow => NetCashFlow > 0;
        public string TrendDirection => NetCashFlow > 0 ? "↗️" : NetCashFlow < 0 ? "↘️" : "➡️";
    }
    
    public class WorkingCapitalComponent
    {
        public string ComponentName { get; set; } = string.Empty;
        public decimal BeginningBalance { get; set; }
        public decimal EndingBalance { get; set; }
        public decimal Change => EndingBalance - BeginningBalance;
        public decimal ChangePercent => BeginningBalance != 0 ? (Change / Math.Abs(BeginningBalance)) * 100 : 0;
        
        // ✅ ADD: Enhanced working capital analysis
        public bool IsImprovement { get; set; }
        public string ComponentType { get; set; } = string.Empty; // "Asset" or "Liability"
        public string ImpactDescription => ComponentType == "Asset" 
            ? (Change > 0 ? "Increased investment" : "Decreased investment")
            : (Change > 0 ? "Increased obligation" : "Decreased obligation");
    }
    
    public class CustomerCashFlow
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public decimal NetCashFlow { get; set; }
        public decimal Collections { get; set; }
        public decimal AverageCollectionDays { get; set; }
        public decimal CollectionEfficiency { get; set; }
        
        // ✅ ADD: Enhanced customer cash flow analysis
        public decimal OutstandingBalance { get; set; }
        public decimal CreditLimit { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public string PaymentTrend { get; set; } = string.Empty;
        public bool IsHighRiskCustomer => CollectionEfficiency < 80 || AverageCollectionDays > 45;
        public string CustomerRating => IsHighRiskCustomer ? "High Risk" : 
                                       CollectionEfficiency > 95 ? "Excellent" :
                                       CollectionEfficiency > 85 ? "Good" : "Average";
    }
    
    // ✅ FIXED: Renamed to avoid confusion with the old CapitalExpenditure class
    public class CapitalExpenditureDetail
    {
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string Category { get; set; } = string.Empty;
        public string FormattedAmount => Amount.ToString("C");
        
        // ✅ ADD: Enhanced capital expenditure tracking
        public string AssetType { get; set; } = string.Empty;
        public int DepreciationYears { get; set; }
        public decimal AnnualDepreciation => DepreciationYears > 0 ? Amount / DepreciationYears : 0;
        public bool IsMaintenanceCapex { get; set; }
        public bool IsGrowthCapex => !IsMaintenanceCapex;
        public string CapexType => IsMaintenanceCapex ? "Maintenance" : "Growth";
    }
    
    // ✅ KEEP: Original CapitalExpenditure for backward compatibility if needed elsewhere
    public class CapitalExpenditure
    {
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        public string Category { get; set; } = string.Empty;
        public string FormattedAmount => Amount.ToString("C");
    }
    
    public class MonthlyProjection
    {
        public DateTime Month { get; set; }
        public decimal ProjectedOperatingCashFlow { get; set; }
        public decimal ProjectedInvestingCashFlow { get; set; }
        public decimal ProjectedFinancingCashFlow { get; set; }
        public decimal ProjectedNetCashFlow { get; set; }
        public decimal ProjectedCashBalance { get; set; }
        public string MonthName => Month.ToString("MMM yyyy");
        
        // ✅ ADD: Projection confidence and variance
        public decimal ConfidenceLevel { get; set; } = 70; // Percentage
        public decimal VarianceRange { get; set; } = 10; // Plus/minus percentage
        public string ProjectionNotes { get; set; } = string.Empty;
        public string ConfidenceDescription => ConfidenceLevel >= 90 ? "High" :
                                              ConfidenceLevel >= 70 ? "Medium" : "Low";
    }
    
    public class ProjectionScenario
    {
        public string ScenarioName { get; set; } = string.Empty;
        public List<MonthlyProjection> Projections { get; set; } = new();
        public decimal TotalProjectedCashFlow { get; set; }
        public decimal MinimumCashBalance { get; set; }
        public DateTime? CashShortfallDate { get; set; }
        
        // ✅ ADD: Scenario analysis enhancements
        public decimal ProbabilityPercent { get; set; }
        public string KeyAssumptions { get; set; } = string.Empty;
        public string RiskFactors { get; set; } = string.Empty;
        public decimal MaxCashBalance { get; set; }
        public DateTime? PeakCashDate { get; set; }
        public bool HasCashShortfall => CashShortfallDate.HasValue;
    }
    
    public class MonthlyCollectionTrend
    {
        public DateTime Month { get; set; }
        public decimal Collections { get; set; }
        public decimal OutstandingAR { get; set; }
        public decimal CollectionRate { get; set; }
        public string MonthName => Month.ToString("MMM yyyy");
        
        // ✅ ADD: Enhanced collection analysis
        public decimal BadDebtWriteOffs { get; set; }
        public decimal NewSales { get; set; }
        public decimal DaysOutstanding { get; set; }
        public bool IsSeasonalPeak { get; set; }
        public string CollectionQuality => CollectionRate > 95 ? "Excellent" : 
                                          CollectionRate > 85 ? "Good" : 
                                          CollectionRate > 75 ? "Fair" : "Poor";
    }
    
    public enum RatioBenchmark
    {
        Excellent,
        Good,
        Average,
        BelowAverage,
        Poor
    }
    
    // ✅ ADD: Additional supporting enums for enhanced analytics
    public enum CashFlowHealthStatus
    {
        Excellent,
        Good,
        Concerning,
        Critical
    }
    
    public enum ProjectionAccuracy
    {
        High,      // 90%+ confidence
        Medium,    // 70-90% confidence
        Low        // <70% confidence
    }
    
    public enum TrendDirection
    {
        Improving,
        Stable,
        Declining
    }
    
    // ✅ ADD: Cash flow alert system
    public class CashFlowAlert
    {
        public string AlertType { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty; // "Info", "Warning", "Critical"
        public DateTime AlertDate { get; set; }
        public bool RequiresAction { get; set; }
        public string RecommendedAction { get; set; } = string.Empty;
        public string IconClass => Severity.ToLower() switch
        {
            "critical" => "fas fa-exclamation-triangle text-danger",
            "warning" => "fas fa-exclamation-circle text-warning",
            _ => "fas fa-info-circle text-info"
        };
    }
    
    // ✅ ADD: Cash flow KPI tracking
    public class CashFlowKPI
    {
        public string KPIName { get; set; } = string.Empty;
        public decimal CurrentValue { get; set; }
        public decimal TargetValue { get; set; }
        public decimal PriorValue { get; set; }
        public string Unit { get; set; } = string.Empty;
        
        public bool IsOnTarget => Math.Abs(CurrentValue - TargetValue) / Math.Max(Math.Abs(TargetValue), 1) <= 0.1m;
        
        public string FormattedCurrent => Unit == "%" ? $"{CurrentValue:F1}%" : 
                                         Unit == "C" ? CurrentValue.ToString("C") : 
                                         CurrentValue.ToString("N0");
        
        public string TrendIndicator => CurrentValue > PriorValue ? "🔼" : 
                                       CurrentValue < PriorValue ? "🔽" : "➡️";
        
        public decimal VarianceFromTarget => CurrentValue - TargetValue;
        public decimal VariancePercent => TargetValue != 0 ? (VarianceFromTarget / Math.Abs(TargetValue)) * 100 : 0;
    }
}