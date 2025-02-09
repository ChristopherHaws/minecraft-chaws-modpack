<Query Kind="Program">
  <NuGetReference>Markdig</NuGetReference>
  <NuGetReference>RestSharp</NuGetReference>
  <NuGetReference>Tomlyn</NuGetReference>
  <Namespace>Markdig</Namespace>
  <Namespace>System.Runtime.Serialization</Namespace>
  <Namespace>System.Text.Json</Namespace>
  <Namespace>System.Text.Json.Serialization</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>Tomlyn</Namespace>
  <Namespace>Tomlyn.Model</Namespace>
</Query>

void Main() {
	Modpack.DetectPackFilePath().Dump("Modpack.DetectPackFilePath()");
	Modpack.DetectRootDirectoryPath().Dump("Modpack.DetectRootDirectoryPath()");
	Modpack.Open().GetModsDirectory().Dump("Modpack.GetModsDirectory()");
}

public class Modpack {
	private string packfilePath;
	private string directoryPath;
	
	private Modpack(string packfilePath) {
		this.packfilePath = packfilePath;
		this.directoryPath = Path.GetDirectoryName(packfilePath)
			?? throw new("Packfile path is not valid");
	}

	public string PackfilePath => this.packfilePath;
	public string DirectoryPath => this.directoryPath;

	public string GetModsDirectory() {
		return Path.Combine(this.directoryPath, "mods");
	}

	public IEnumerable<string> GetModFiles() {
		var modsDirectory = this.GetModsDirectory();
		return Directory.EnumerateFiles(modsDirectory, "*.pw.toml");
	}

	public Task<PackwizMod[]> GetMods() {
		return PackwizMod.ReadFromFiles(GetModFiles());
	}

	public static Modpack Open() {
		return Open(DetectPackFilePath());
	}
	
	public static Modpack Open(string packfilePath) {
		if (!File.Exists(packfilePath)) {
			throw new("Invalid packfile path: " + packfilePath);
		}
		
		return new(packfilePath);
	}

	public static string DetectRootDirectoryPath() {
		var currentQueryPath = Util.CurrentQueryPath ?? throw new("Current query has not been saved yet");
		var currentDirectory = Directory.GetParent(currentQueryPath) ?? throw new("Could not determine the directory of the current query");

		while (!currentDirectory.GetFiles().Any(x => x.Name == "pack.toml")) {
			currentDirectory = currentDirectory.Parent ?? throw new("Directory does not have a parent directory" + currentDirectory.FullName);
		}

		return currentDirectory.FullName;
	}

	public static string DetectPackFilePath() {
		return Path.Combine(DetectRootDirectoryPath(), "pack.toml");
	}
}

// https://packwiz.infra.link/reference/pack-format/mod-toml/
public class PackwizMod {
	[IgnoreDataMember]
	public string FullFilePath { get; set; } = default!;

	[DataMember(Name = "name")]
	public string Name { get; set; } = default!;
	[DataMember(Name = "filename")]
	public string FileName { get; set; } = default!;
	[DataMember(Name = "side")]
	public string Side { get; set; } = default!;

	[DataMember(Name = "download")]
	public Download Download { get; set; } = default!;
	[DataMember(Name = "update")]
	public Update? Update { get; set; }
	[DataMember(Name = "option")]
	public Option? Option { get; set; }

	public string ModpackRelativeFilePath => Path.GetRelativePath(Modpack.DetectRootDirectoryPath(), this.FullFilePath);
	public string Slug => Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(this.FullFilePath));
	public bool IsRequired => this.Option is null ? true : !this.Option.Optional;

	public (
		int Id,
		string Url,
		string FileUrl
	)? CurseForge => this.Update?.CurseForge is null ? null : (
		Id: this.Update.CurseForge.ProjectId,
		Url: @$"https://www.curseforge.com/minecraft/mc-mods/{this.Slug}",
		FileUrl: @$"https://www.curseforge.com/minecraft/mc-mods/{this.Slug}/files/{this.Update.CurseForge.FileId}"
	);

	public (
		string Id,
		string Url,
		string FileUrl
	)? Modrinth => this.Update?.Modrinth is null ? null : (
		Id: this.Update.Modrinth.ModId,
		Url: @$"https://modrinth.com/mod/{this.Update.Modrinth.ModId}",
		FileUrl: @$"https://modrinth.com/mod/{this.Update.Modrinth.ModId}/version/{this.Update.Modrinth.Version}"
	);

	public static async Task<PackwizMod> ReadFromFile(string path) {
		var file = await File.ReadAllTextAsync(path);
		var mod = Toml.ToModel<PackwizMod>(file);
		mod.FullFilePath = path;
		return mod;
	}

	public static async Task<PackwizMod[]> ReadFromFiles(IEnumerable<string> paths) {
		var mods = new List<PackwizMod>();

		foreach (var path in paths) {
			var mod = await PackwizMod.ReadFromFile(path);
			mods.Add(mod);
		}

		return mods.ToArray();
	}
}

public class Download {
	[DataMember(Name = "hash-format")]
	public string HashFormat { get; set; } = default!;
	[DataMember(Name = "hash")]
	public string Hash { get; set; } = default!;
	[DataMember(Name = "url")]
	public string? Url { get; set; }
	[DataMember(Name = "mode")]
	public string? Mode { get; set; }
}

public class Update {
	[DataMember(Name = "curseforge")]
	public CurseForgeUpdate? CurseForge { get; set; }
	[DataMember(Name = "modrinth")]
	public ModrinthUpdate? Modrinth { get; set; }
}

public class CurseForgeUpdate {
	[DataMember(Name = "file-id")]
	public int FileId { get; set; } = default!;
	[DataMember(Name = "project-id")]
	public int ProjectId { get; set; } = default!;
	[DataMember(Name = "release-channel")]
	public string? ReleaseChannel { get; set; }
}

public class ModrinthUpdate {
	[DataMember(Name = "mod-id")]
	public string ModId { get; set; } = default!;
	[DataMember(Name = "version")]
	public string Version { get; set; } = default!;
}

public class Option {
	[DataMember(Name = "optional")]
	public bool Optional { get; set; }
	[DataMember(Name = "default")]
	public bool? Default { get; set; }
	[DataMember(Name = "description")]
	public string? Description { get; set; }
}
