using IssueTest.ViewModels;

namespace IssueTest.Views;

public partial class ItemCollectionView : ContentView
{
	public ItemCollectionView()
	{
		InitializeComponent();
	}

    private void Button_Clicked(object sender, EventArgs e)
    {
		(BindingContext as MainFormViewModel).ProcessClick();
    }
}