namespace PaymentsService.Models;

/// <summary>
/// ��� ������� ������
/// </summary>
public enum OrderStatusType
{
	/// <summary>
	/// �� �������
	/// </summary>
	Unpaid,

	/// <summary>
	/// ������ ������ ���������
	/// </summary>
	Declined,

	/// <summary>
	/// �������
	/// </summary>
	Paid,
}