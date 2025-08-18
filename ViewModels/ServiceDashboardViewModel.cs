using InventorySystem.Models;
using System.Collections.Generic;
namespace InventorySystem.ViewModels
{
	public class ServiceDashboardViewModel
	{
		// Quick Stats
		public int TotalActiveServices { get; set; }
		public int ServicesScheduledToday { get; set; }
		public int OverdueServices { get; set; }
		public int EmergencyServices { get; set; }
		public decimal MonthlyRevenue { get; set; }
		public decimal AvgServiceTime { get; set; }

		// Recent Activity
		public List<ServiceOrder> RecentServiceOrders { get; set; } = new();
		public List<ServiceOrder> TodaysSchedule { get; set; } = new();
		public List<ServiceOrder> OverdueList { get; set; } = new();

		// Charts Data
		public Dictionary<string, int> ServicesByStatus { get; set; } = new();
		public Dictionary<string, int> ServicesByPriority { get; set; } = new();
		public List<ServiceTrendData> RecentTrends { get; set; } = new();

		// Performance Metrics
		public decimal AvgCompletionTime { get; set; }
		public decimal OnTimeCompletionRate { get; set; }
		public decimal CustomerSatisfactionRate { get; set; }
		public Dictionary<string, decimal> TechnicianUtilization { get; set; } = new();

		// Alerts and Notifications
		public List<string> SystemAlerts { get; set; } = new();
		public int PendingApprovals { get; set; }
		public int ServicesAwaitingParts { get; set; }
	}
}
