"""Exercise output paths in the real shell scripts without requiring Unity."""
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import textwrap
import unittest


class OutputPathsTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve() / "project with spaces"
        (self.root / "tools").mkdir(parents=True)
        settings = self.root / "unity/ProjectSettings"
        settings.mkdir(parents=True)
        (settings / "ProjectVersion.txt").write_text("m_EditorVersion: test\n")
        source = Path(__file__).resolve().parents[1]
        for name in ("test.sh", "verify.sh", "build.sh", "run.sh"):
            shutil.copy2(source / name, self.root / "tools" / name)
        self.launched = self.root / "launched"
        stub = self.root / "fake-unity"
        stub.write_text(textwrap.dedent('''\
            #!/usr/bin/env python3
            import os
            from pathlib import Path
            import sys

            args = sys.argv[1:]
            def value(flag):
                return args[args.index(flag) + 1]

            Path(os.environ["LAUNCHED"]).touch()
            log = Path(value("-logFile"))
            log.parent.mkdir(parents=True, exist_ok=True)
            if "-runTests" in args:
                # Unity Test Framework resolves relative results against projectPath.
                results = Path(value("-testResults"))
                if not results.is_absolute():
                    results = Path(value("-projectPath")) / results
                results.parent.mkdir(parents=True, exist_ok=True)
                results.write_text('<test-run total="1" passed="1" failed="0"/>')
                log.write_text("tests passed\\n")
            elif "-executeMethod" in args:
                log.write_text("production art validation passed\\nvolume turns passed\\n")
            else:
                log.write_text("[DeepFeast] exit: passed\\n")
            '''))
        stub.chmod(0o755)
        self.env = dict(os.environ, UNITY=str(stub), PLAYER=str(stub),
                        LAUNCHED=str(self.launched))

    def run_script(self, name, *args, cwd=None):
        return subprocess.run(
            [str(self.root / "tools" / name), *map(str, args)],
            cwd=cwd or self.root, env=self.env, text=True,
            stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=20)

    def assert_passed(self, result):
        self.assertEqual(result.returncode, 0, result.stdout)

    def test_relative_results_from_another_working_directory(self):
        caller = self.root / "caller"
        caller.mkdir()
        self.assert_passed(self.run_script("test.sh", "results with spaces", cwd=caller))
        self.assertTrue((caller / "results with spaces/editmode-results.xml").is_file())

    def test_absolute_results(self):
        out = self.root / "absolute results"
        self.assert_passed(self.run_script("test.sh", out))
        self.assertTrue((out / "editmode-results.xml").is_file())

    def test_default_results(self):
        self.assert_passed(self.run_script("test.sh"))
        self.assertTrue((self.root / "unity/Logs/editmode-results.xml").is_file())

    def test_verify_relative_output_and_owned_directory_reuse(self):
        for _ in range(2):
            self.assert_passed(self.run_script("verify.sh", "verification results"))
        out = self.root / "verification results"
        self.assertTrue((out / "editmode-results.xml").is_file())
        self.assertTrue((out / ".deepfeast-verify").is_file())

    def test_verify_preserves_existing_file(self):
        out = self.root / "report.txt"
        out.write_text("keep this report\n")
        result = self.run_script("verify.sh", out, "--no-build")
        self.assertEqual(result.returncode, 2, result.stdout)
        self.assertTrue(out.is_file(), "Existing report was replaced")
        self.assertEqual(out.read_text(), "keep this report\n")
        self.assertFalse(self.launched.exists())

    def test_verify_preserves_unowned_directory(self):
        out = self.root / "unowned"
        out.mkdir()
        (out / "report.txt").write_text("keep this report\n")
        result = self.run_script("verify.sh", out, "--no-build")
        self.assertEqual(result.returncode, 2, result.stdout)
        self.assertEqual((out / "report.txt").read_text(), "keep this report\n")
        self.assertFalse(self.launched.exists())

    def test_verify_preserves_file_symlinks(self):
        target = self.root / "report.txt"
        target.write_text("keep this report\n")
        for name, destination in (("file-link", target),
                                  ("broken-link", self.root / "missing")):
            with self.subTest(name=name):
                out = self.root / name
                out.symlink_to(destination)
                result = self.run_script("verify.sh", out, "--no-build")
                self.assertEqual(result.returncode, 2, result.stdout)
                self.assertTrue(out.is_symlink())
                self.assertEqual(target.read_text(), "keep this report\n")
                self.assertFalse(self.launched.exists())

    def test_test_script_rejects_invalid_output_before_launch(self):
        out = self.root / "report.txt"
        out.write_text("keep this report\n")
        result = self.run_script("test.sh", out)
        self.assertEqual(result.returncode, 2, result.stdout)
        self.assertEqual(out.read_text(), "keep this report\n")
        self.assertFalse(self.launched.exists())

    def test_verify_accepts_empty_directory(self):
        out = self.root / "empty"
        out.mkdir()
        self.assert_passed(self.run_script("verify.sh", out, "--no-build"))


if __name__ == "__main__":
    unittest.main()
