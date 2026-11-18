#!/usr/bin/env python3
"""Run the cached pinned Room/KSP processor without Gradle. No Android runtime check."""
from pathlib import Path
import subprocess,os,zipfile
repo=Path(__file__).resolve().parents[1]/'App/MealPrepApp.Android'
work=repo/'app/build/room-cached';work.mkdir(parents=True,exist_ok=True)
cache=Path.home()/'.gradle/caches/modules-2/files-2.1'
def jar(group,artifact,version=None):return next(((cache/group/artifact/version) if version else (cache/group/artifact)).rglob('*.jar'))
jdk=Path(os.environ['JAVA_HOME'])
stdlib=jar('org.jetbrains.kotlin','kotlin-stdlib','2.1.20')
api=jar('com.google.devtools.ksp','symbol-processing-api','2.1.20-1.0.32')
compiler=[jar('org.jetbrains.kotlin','kotlin-compiler-embeddable','2.1.20'),stdlib,jar('org.jetbrains.kotlin','kotlin-script-runtime','2.1.20'),jar('org.jetbrains.kotlin','kotlin-reflect'),jar('org.jetbrains.intellij.deps','trove4j'),jar('org.jetbrains','annotations','13.0'),jar('org.jetbrains.kotlinx','kotlinx-coroutines-core-jvm'),api]
compiler += list((cache/'org.jetbrains.kotlinx').rglob('*.jar'))
android=Path(os.environ['ANDROID_HOME'])/'platforms/android-35/android.jar'
cp=[android,stdlib]+[p for p in cache.rglob('*.jar') if p.relative_to(cache).parts[0].startswith('androidx.') or p.relative_to(cache).parts[0]=='org.jetbrains.kotlinx']+[p for p in (Path.home()/'.gradle/caches/8.11.1/transforms').rglob('classes.jar') if 'instrumented' not in str(p)]
processor=[p for p in cache.rglob('*.jar') if p.relative_to(cache).parts[0] in ['androidx.room','com.google.auto','com.google.auto.value','com.squareup','com.google.guava','org.xerial','commons-codec','com.google.code.findbugs','org.checkerframework','com.google.errorprone','com.google.j2objc','org.jetbrains.kotlinx']]+[stdlib,api]
processor=[jar('com.squareup','javapoet','1.13.0'),jar('com.squareup','kotlinpoet-jvm','1.17.0'),jar('com.squareup','kotlinpoet-javapoet','1.17.0')]+[p for p in processor if p.parent.parent.parent.name not in ['javapoet','kotlinpoet-jvm','kotlinpoet-javapoet']] + list((cache/'com.google.code.gson').rglob('*.jar'))
args=[jdk/'bin/java','-Xmx1200m','-cp',os.pathsep.join(map(str,compiler)),'org.jetbrains.kotlin.cli.jvm.K2JVMCompiler','-no-stdlib','-no-reflect','-jvm-target','17','-language-version','1.9','-Xskip-metadata-version-check','-classpath',os.pathsep.join(map(str,cp)),'-Xplugin='+str(jar('com.google.devtools.ksp','symbol-processing','2.1.20-1.0.32')),'-d',work/'classes']
options={'classOutputDir':work/'classes','javaOutputDir':work/'java','kotlinOutputDir':work/'kotlin','resourceOutputDir':work/'resources','cachesDir':work/'cache','projectBaseDir':repo,'kspOutputDir':work,'apclasspath':os.pathsep.join(map(str,processor)),'apoption':'room.schemaLocation='+str(repo/'app/schemas'),'incremental':'false','withCompilation':'false'}
for key,value in options.items():args += ['-P','plugin:com.google.devtools.ksp.symbol-processing:'+key+'='+str(value)]
args+=[repo/'app/src/main/java/app/mealprep/companion/ShoppingDatabase.kt']
r=subprocess.run(list(map(str,args)),cwd=repo,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True)
(work/'result.log').write_text(r.stdout)
print(r.stdout[-12000:]);raise SystemExit(r.returncode)
