// SPDX-License-Identifier: GPL-3.0-or-later
// Uses the unmodified HMCL installation engine. Corresponding source is bundled.
import java.nio.file.*;
import javafx.application.Platform;
import org.jackhuang.hmcl.download.*;
import org.jackhuang.hmcl.game.*;
import org.jackhuang.hmcl.task.Schedulers;
import org.jackhuang.hmcl.task.Task;
import org.jackhuang.hmcl.util.CacheRepository;

class MineDockBridge {
    private static <T> T run(Task<T> task) throws Exception {
        var executor = task.executor();
        if (!executor.test()) {
            if (executor.getException() != null) throw executor.getException();
            throw new IllegalStateException("Installation task was cancelled");
        }
        return task.getResult();
    }
    public static void main(String[] args) {
        int result = 1;
        try {
            if (args.length != 6) throw new IllegalArgumentException("root game loader version target source");
            Platform.startup(() -> {});
            Platform.setImplicitExit(false);
            Path root = Path.of(args[0]).toAbsolutePath();
            String game = args[1], loader = args[2], version = args[3], target = args[4];
            var repository = new DefaultGameRepository(root);
            repository.refreshVersions();
            DownloadProvider official = new MojangDownloadProvider() {
                @Override public String injectURL(String url) {
                    // LiteLoader metadata points ASM at an unavailable Mojang repository path.
                    String prefix = "https://libraries.minecraft.net/org/ow2/asm/";
                    return url.startsWith(prefix)
                            ? "https://repo.maven.apache.org/maven2/org/ow2/asm/" + url.substring(prefix.length())
                            : url;
                }
            };
            DownloadProvider mirror = new BMCLAPIDownloadProvider("https://bmclapi2.bangbang93.com");
            DownloadProvider provider = switch (args[5]) {
                case "Official" -> official;
                case "Bmclapi" -> mirror;
                default -> new AutoDownloadProvider(mirror, official);
            };
            var cache = new DefaultCacheRepository(root.resolve(".engine-cache"));
            CacheRepository.setInstance(cache);
            var manager = new DefaultDependencyManager(repository, provider, cache);
            Version base = new Version(target).setInheritsFrom(game).setJar(game);
            System.out.println("MINEDOCK: installing " + loader + " " + version);
            // LiteLoader's discontinued index is mirrored; library downloads still honor the selected source.
            var versions = (loader.equals("liteloader") ? mirror : provider).getVersionListById(loader);
            run(versions.loadAsync(game));
            RemoteVersion selected = versions.getVersion(game, version).orElseThrow(
                    () -> new IllegalArgumentException("No compatible " + loader + " " + version + " for " + game));
            Version installed = run(manager.installLibraryAsync(base, selected));
            run(repository.saveAsync(installed));
            repository.refreshVersions();
            run(manager.checkLibraryCompletionAsync(repository.getVersion(target).resolve(repository), true));
            if (!Files.isRegularFile(repository.getVersionJson(target)))
                throw new IllegalStateException("Installer did not create a launch profile");
            System.out.println("MINEDOCK: complete");
            result = 0;
        } catch (Throwable failure) {
            failure.printStackTrace(System.err);
        } finally {
            Schedulers.shutdown();
            Platform.exit();
        }
        System.exit(result);
    }
}
