# Unity Property History Tool

![Property History Window](https://user-images.githubusercontent.com/AlirezaF80/Unity-Property-History/Preview.png)

A Unity editor tool that allows you to view the Git history of component properties or asset fields directly within the inspector. Track down when a value changed, who changed it, and why, without leaving the Unity Editor. 

This is more of a cool **proof of concept** and does not cover all edge cases. Use it at your own risk.

## Features

-   **View Git History:** Right-click on a property in the Inspector to see its complete history.
-   **Detailed Commit Info:** See the commit hash, author, message, and the value of the property at each change.

## Requirements

-   Unity 2021.3, or later (May work with earlier versions, but not tested).
-   **Git** must be installed and accessible from the command line (in your system's PATH).
-   The project must be a Git repository.

## Installation

You can install this tool via the Unity Package Manager using a Git URL:

1.  In Unity, open the **Package Manager** (`Window > Package Manager`).
2.  Click the **"+"** button in the top-left corner.
3.  Select **"Add package from git URL..."**.
4.  Enter the following URL and click **"Add"**:
    `https://github.com/alirezaf80/unity-property-history.git`

## How to Use

1.  Click on a component in the Inspector.
2.  **Right-click** on the label of any property (e.g., `Position`, `Scale`, `My Custom Field`, etc.).
3.  From the context menu, select **"Show Property History Window"**.
4.  A new window will open, displaying the complete commit history for that specific property.

## Known Issues & Limitations

-   **Not All Properties Supported:** Some properties, especially those that are part of complex types, may not show history correctly.
-   **Git Only:** This tool only works with Git repositories. Other version control systems are not supported.
-   **Asset Importers Not Supported:** Viewing the history of properties within an Asset's import settings (TextureImporter, ModelImporter, etc.) is not supported.
-   **Performance on Large Files:** For assets with a very long and complex Git history, loading the history window may take a few moments. The process runs in the background to avoid freezing the editor.
-   **Nested Prefabs:** The tool may not correctly track changes made to properties within nested prefabs.

## Future Improvements
-   Support for viewing history of properties in Asset Importers.
-   Better handling of nested prefabs.
-   Performance optimizations for large files and repositories.
-   More robust error handling and user feedback.
-   Support for other version control systems.

## Contributing
Contributions are welcome! If you find a bug or have a feature request, please open an issue on the [GitHub repository](https://github.com/alirezaf80/unity-property-history). Pull requests are also encouraged.

## License

This project is licensed under the MIT License. See the [LICENSE.md](LICENSE.md) file for details.