using System.Reactive.Disposables;
using ReactiveUI;

namespace San11PVPToolClient.ViewModels;

public class ViewModelBase : ReactiveObject, IActivatableViewModel
{
    public ViewModelBase()
    {
        this.WhenActivated(DoWhenActivated);
    }

    public ViewModelActivator Activator { get; } = new();

    protected virtual void DoWhenActivated(CompositeDisposable disposable) { }
}
