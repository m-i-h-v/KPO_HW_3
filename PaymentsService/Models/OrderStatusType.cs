namespace PaymentsService.Models;

/// <summary>
/// Тип статуса заказа
/// </summary>
public enum OrderStatusType
{
	/// <summary>
	/// Не оплачен
	/// </summary>
	Unpaid,

	/// <summary>
	/// Оплата заказа отклонена
	/// </summary>
	Declined,

	/// <summary>
	/// Оплачен
	/// </summary>
	Paid,
}