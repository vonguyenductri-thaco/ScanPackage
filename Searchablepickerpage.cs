using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;

namespace ScanPackage;

public class SearchablePickerPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _tcs;
    private readonly List<string> _allItems;
    private List<string> _filteredItems;
    private readonly SearchBar _searchBar;
    private readonly CollectionView _collectionView;

    public SearchablePickerPage(string title, List<string> items, TaskCompletionSource<string?> tcs)
    {
        _tcs = tcs;
        _allItems = items;
        _filteredItems = new List<string>(items);

        Title = title;
        BackgroundColor = Colors.White;

        _searchBar = new SearchBar
        {
            Placeholder = $"Tìm {title.ToLower()}...",
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            TextColor = Color.FromArgb("#212121"),
            PlaceholderColor = Color.FromArgb("#9E9E9E"),
            Margin = new Thickness(10, 10, 10, 5)
        };
        _searchBar.TextChanged += OnSearchTextChanged;

        _collectionView = new CollectionView
        {
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label
                {
                    FontSize = 16,
                    TextColor = Color.FromArgb("#212121"),
                    VerticalTextAlignment = TextAlignment.Center,
                    Padding = new Thickness(20, 15)
                };
                label.SetBinding(Label.TextProperty, ".");

                var tapGesture = new TapGestureRecognizer();
                tapGesture.Tapped += OnItemTapped;
                label.GestureRecognizers.Add(tapGesture);

                var frame = new Frame
                {
                    Content = label,
                    Padding = 0,
                    Margin = new Thickness(10, 2),
                    BorderColor = Color.FromArgb("#E0E0E0"),
                    CornerRadius = 8,
                    HasShadow = false
                };

                return frame;
            }),
            ItemsSource = _filteredItems
        };

        var cancelButton = new Button
        {
            Text = "Hủy",
            BackgroundColor = Color.FromArgb("#757575"),
            TextColor = Colors.White,
            Margin = new Thickness(10, 5, 10, 10),
            CornerRadius = 8,
            HeightRequest = 50
        };
        cancelButton.Clicked += OnCancelClicked;

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto }
            },
            Children =
            {
                _searchBar,
                _collectionView,
                cancelButton
            }
        };

        Grid.SetRow(_searchBar, 0);
        Grid.SetRow(_collectionView, 1);
        Grid.SetRow(cancelButton, 2);
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var searchText = e.NewTextValue?.Trim().ToLower() ?? "";

        if (string.IsNullOrEmpty(searchText))
        {
            _filteredItems = new List<string>(_allItems);
        }
        else
        {
            _filteredItems = _allItems
                .Where(item => item.ToLower().Contains(searchText))
                .ToList();
        }

        _collectionView.ItemsSource = _filteredItems;
    }

    private async void OnItemTapped(object? sender, EventArgs e)
    {
        if (sender is Label label && label.Text is string selectedItem)
        {
            _tcs.TrySetResult(selectedItem);
            await Navigation.PopModalAsync();
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        _tcs.TrySetResult(null);
        await Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _tcs.TrySetResult(null);
        return base.OnBackButtonPressed();
    }
}