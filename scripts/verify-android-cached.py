#!/usr/bin/env python3
"""Resource/Kotlin/JVM verification using cached dependencies; not an APK or Android runtime check."""
from pathlib import Path
import os, sys, re, hashlib, json, shutil, subprocess, zipfile, xml.etree.ElementTree as ET
repo=Path(__file__).resolve().parents[1]/'App/MealPrepApp.Android'
work=repo/'app/build/cached-verification'
work.mkdir(parents=True,exist_ok=True)
for output in ['main','tests','android-tests','java','r']:
 shutil.rmtree(work/output,ignore_errors=True)
def source_hashes():
 paths=sorted((repo/'app/src').rglob('*'))+sorted((repo/'tools').glob('*'))+[repo/'app/build.gradle.kts']
 return {str(p.relative_to(repo)):hashlib.sha256(p.read_bytes()).hexdigest() for p in paths if p.is_file()}
source_before=source_hashes()
cache=Path.home()/'.gradle/caches/modules-2/files-2.1'
jdk=Path(os.environ['JAVA_HOME'])/'bin'
android=Path(os.environ['ANDROID_HOME'])/'platforms/android-35/android.jar'
def jar(group, artifact, version=None):
 base=cache/group/artifact
 return next((base/version if version else base).rglob('*.jar'))
def run(label,args):
 print(label,flush=True)
 result=subprocess.run([str(x) for x in args],cwd=repo,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
 (work/(label+'.log')).write_text(result.stdout)
 print(result.stdout[-14000:] if result.returncode else result.stdout[-3000:],flush=True)
 if result.returncode: raise SystemExit(result.returncode)
if '--skip-room' not in sys.argv:
 run('room-codegen',[os.environ.get('PYTHON','python3'),repo.parents[1]/'scripts/verify-android-room-cached.py'])
version=(repo/'app/build.gradle.kts').read_text()
version_code=re.search(r'versionCode = (\d+)',version)[1]
version_name=re.search(r'versionName = "([^"]+)"',version)[1]
# Check localization and compile/link the current app resources over cached merged dependencies.
a=ET.parse(repo/'app/src/main/res/values/strings.xml').getroot()
b=ET.parse(repo/'app/src/main/res/values-ro/strings.xml').getroot()
c=ET.parse(repo/'app/src/main/res/values-ru/strings.xml').getroot()
names={e.attrib['name'] for e in a}
assert len(names)==len(a), 'Duplicate string resource names'
assert names == {e.attrib['name'] for e in b} == {e.attrib['name'] for e in c}
for source in (repo/'app/src').rglob('*.kt'):
 for name in re.findall(r'(?<!android\.)R\.string\.(\w+)',source.read_text()):
  assert name in names, f'{source}: missing string {name}'

print(f'Localization: {len(a)} matching resource names',flush=True)
aapt_jar=jar('com.android.tools.build','aapt2','8.9.2-12782657')
with zipfile.ZipFile(aapt_jar) as z: (work/'aapt2').write_bytes(z.read('aapt2'))
(work/'aapt2').chmod(0o755)
run('resources-compile',[work/'aapt2','compile','--dir',repo/'app/src/main/res','-o',work/'app-res.zip'])
manifest=ET.parse(repo/'app/src/main/AndroidManifest.xml');manifest.getroot().set('package','app.mealprep.companion');manifest.write(work/'AndroidManifest.xml'); (work/'AndroidManifest.xml').write_text((work/'AndroidManifest.xml').read_text().replace('${applicationId}','app.mealprep.companion'))
args=[work/'aapt2','link','-I',android,'--manifest',work/'AndroidManifest.xml','--auto-add-overlay','--java',work/'r','--min-sdk-version','26','--target-sdk-version','35','--version-code',version_code,'--version-name',version_name,'-o',work/'resources.ap_']
for flat in sorted(Path(os.environ.get('MEALPREP_ANDROID_DEPENDENCY_RES',str(repo/'app/build/intermediates/merged_res/debug/mergeDebugResources'))).rglob('*.flat')): args.extend(['-R',flat])
args.extend(['-R',work/'app-res.zip'])
run('resources-link',args)
(work/'java').mkdir(exist_ok=True)
run('resources-java',[jdk/'javac','-source','17','-target','17','-d',work/'java',*sorted((work/'r').rglob('*.java')),*[]])
compiler=jar('org.jetbrains.kotlin','kotlin-compiler-embeddable','2.1.20')
stdlib=jar('org.jetbrains.kotlin','kotlin-stdlib','2.1.20')
serialization=jar('org.jetbrains.kotlin','kotlin-serialization-compiler-plugin-embeddable','2.1.20')
compose=jar('org.jetbrains.kotlin','kotlin-compose-compiler-plugin-embeddable','2.1.20')
compiler_cp=[compiler,stdlib,jar('org.jetbrains.kotlin','kotlin-script-runtime','2.1.20'),jar('org.jetbrains.kotlin','kotlin-reflect'),jar('org.jetbrains.intellij.deps','trove4j'),jar('org.jetbrains','annotations','13.0'),jar('org.jetbrains.kotlinx','kotlinx-coroutines-core-jvm')]
module_jars=[p for p in cache.rglob('*.jar') if (p.relative_to(cache).parts[0].startswith('androidx.') or p.relative_to(cache).parts[0] in ['org.jetbrains.kotlinx','org.jetbrains','junit','org.hamcrest'])]
module_jars.extend([stdlib,jar('org.jetbrains.kotlin','kotlin-stdlib-jdk7'),jar('org.jetbrains.kotlin','kotlin-stdlib-jdk8')])
android_jars=[p for p in (Path.home()/'.gradle/caches/8.11.1/transforms').rglob('classes.jar') if 'instrumented' not in str(p)]
cp=[work/'java',android,*module_jars,*android_jars]
(work/'main').mkdir(exist_ok=True)
common=[jdk/'java','-Xmx1536m','-cp',os.pathsep.join(map(str,compiler_cp)),'org.jetbrains.kotlin.cli.jvm.K2JVMCompiler','-no-stdlib','-no-reflect','-jvm-target','17']
run('kotlin-main',[*common,'-Xplugin='+str(serialization),'-Xplugin='+str(compose),'-classpath',os.pathsep.join(map(str,cp)),'-d',work/'main',*sorted((repo/'app/src/main/java').rglob('*.kt')),*sorted((repo/'app/build/room-cached/kotlin').rglob('*.kt'))])
(work/'tests').mkdir(exist_ok=True)
run('kotlin-tests',[*common,'-Xfriend-paths='+str(work/'main'),'-Xplugin='+str(serialization),'-classpath',os.pathsep.join(map(str,[work/'main',*cp])),'-d',work/'tests',*sorted((repo/'app/src/test/java').rglob('*.kt'))])
(work/'android-tests').mkdir(exist_ok=True)
run('kotlin-android-tests', [*common, '-Xplugin='+str(serialization), '-Xplugin='+str(compose), '-classpath', os.pathsep.join(map(str,[work/'main',*cp])), '-d', work/'android-tests', *sorted((repo/'app/src/androidTest/java').rglob('*.kt'))])

tests=['app.mealprep.companion.'+p.stem for p in sorted((repo/'app/src/test/java/app/mealprep/companion').glob('*Tests.kt'))]
run('junit',[jdk/'java','-cp',os.pathsep.join(map(str,[work/'tests',work/'main',*cp])),'org.junit.runner.JUnitCore',*tests])
assert source_before==source_hashes(), 'Sources changed during verification; rerun against a stable snapshot.'
(work/'source-sha256.json').write_text(json.dumps(source_before,indent=2)+'\n')
print('Resource checks, Kotlin compilation and JVM tests passed. No Gradle build, APK or device run is claimed.',flush=True)
