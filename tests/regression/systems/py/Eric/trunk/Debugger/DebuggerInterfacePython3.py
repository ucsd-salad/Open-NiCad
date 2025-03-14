# -*- coding: utf-8 -*-

# Copyright (c) 2009 - 2010 Detlev Offenbach <detlev@die-offenbachs.de>
#

"""
Module implementing the Python3 debugger interface for the debug server.
"""

import sys
import os
import socket
import subprocess

from PyQt4.QtCore import *

from KdeQt import KQMessageBox, KQInputDialog
from KdeQt.KQApplication import e4App

from DebugProtocol import *
import DebugClientCapabilities

import Preferences
import Utilities

from eric4config import getConfig


##ClientDefaultCapabilities = DebugClientCapabilities.HasAll
ClientDefaultCapabilities = \
    DebugClientCapabilities.HasDebugger | \
    DebugClientCapabilities.HasInterpreter | \
    DebugClientCapabilities.HasShell | \
    DebugClientCapabilities.HasCompleter | \
    DebugClientCapabilities.HasProfiler | \
    DebugClientCapabilities.HasUnittest
    
def getRegistryData():
    """
    Module function to get characterising data for the debugger interface.
    
    @return list of the following data. Client type (string), client 
        capabilities (integer), client type association (list of strings)
    """
    exts = []
    for ext in unicode(Preferences.getDebugger("Python3Extensions")).split():
        if ext.startswith("."):
            exts.append(ext)
        else:
            exts.append(".%s" % ext)
    
    if exts and unicode(Preferences.getDebugger("Python3Interpreter")):
        return ["Python3", ClientDefaultCapabilities, exts]
    else:
        return ["", 0, []]

class DebuggerInterfacePython3(QObject):
    """
    Class implementing the Python debugger interface for the debug server.
    """
    def __init__(self, debugServer, passive):
        """
        Constructor
        
        @param debugServer reference to the debug server (DebugServer)
        @param passive flag indicating passive connection mode (boolean)
        """
        QObject.__init__(self)
        
        self.__isNetworked = True
        self.__autoContinue = not passive
        
        self.debugServer = debugServer
        self.passive = passive
        self.process = None
        
        self.qsock = None
        self.queue = []
        # set default values for capabilities of clients
        self.clientCapabilities = ClientDefaultCapabilities
        
        self.codec = QTextCodec.codecForName(str(Preferences.getSystem("StringEncoding")))
        
        if passive:
            # set translation function
            if Preferences.getDebugger("PathTranslation"):
                self.translateRemote = \
                    unicode(Preferences.getDebugger("PathTranslationRemote"))
                self.translateLocal = \
                    unicode(Preferences.getDebugger("PathTranslationLocal"))
                self.translate = self.__remoteTranslation
            else:
                self.translate = self.__identityTranslation

    def __identityTranslation(self, fn, remote2local = True):
        """
        Private method to perform the identity path translation.
        
        @param fn filename to be translated (string or QString)
        @param remote2local flag indicating the direction of translation
            (False = local to remote, True = remote to local [default])
        @return translated filename (string)
        """
        return unicode(fn)
        
    def __remoteTranslation(self, fn, remote2local = True):
        """
        Private method to perform the path translation.
        
        @param fn filename to be translated (string or QString)
        @param remote2local flag indicating the direction of translation
            (False = local to remote, True = remote to local [default])
        @return translated filename (string)
        """
        if remote2local:
            return unicode(fn).replace(self.translateRemote, self.translateLocal)
        else:
            return unicode(fn).replace(self.translateLocal, self.translateRemote)
        
    def __startProcess(self, program, arguments, environment = None):
        """
        Private method to start the debugger client process.
        
        @param program name of the executable to start (string)
        @param arguments arguments to be passed to the program (list of string)
        @param environment dictionary of environment settings to pass (dict of string)
        @return the process object (QProcess) or None
        """
        proc = QProcess()
        if environment is not None:
            env = QStringList()
            for key, value in environment.items():
                env.append("%s=%s" % (key, value))
            proc.setEnvironment(env)
        args = QStringList()
        for arg in arguments:
            args.append(arg)
        proc.start(program, args)
        if not proc.waitForStarted(10000):
            proc = None
        
        return proc
        
    def startRemote(self, port, runInConsole):
        """
        Public method to start a remote Python interpreter.
        
        @param port portnumber the debug server is listening on (integer)
        @param runInConsole flag indicating to start the debugger in a 
            console window (boolean)
        @return client process object (QProcess) and a flag to indicate
            a network connection (boolean)
        """
        interpreter = unicode(Preferences.getDebugger("Python3Interpreter"))
        if interpreter == "":
            KQMessageBox.critical(None,
                self.trUtf8("Start Debugger"),
                self.trUtf8(\
                    """<p>No Python3 interpreter configured.</p>"""))
            return None, False
        
        debugClientType = unicode(Preferences.getDebugger("DebugClientType3"))
        if debugClientType == "standard":
            debugClient = os.path.join(getConfig('ericDir'), 
                                       "DebugClients", "Python3", "DebugClient.py")
        elif debugClientType == "threaded":
            debugClient = os.path.join(getConfig('ericDir'),
                                       "DebugClients", "Python3", "DebugClientThreads.py")
        else:
            debugClient = unicode(Preferences.getDebugger("DebugClient3"))
            if debugClient == "":
                debugClient = os.path.join(sys.path[0],
                                           "DebugClients", "Python3", "DebugClient.py")
        
        redirect = str(Preferences.getDebugger("Python3Redirect"))
        noencoding = \
            Preferences.getDebugger("Python3NoEncoding") and '--no-encoding' or ''
        
        if Preferences.getDebugger("RemoteDbgEnabled"):
            ipaddr = self.debugServer.getHostAddress(False)
            rexec = unicode(Preferences.getDebugger("RemoteExecution"))
            rhost = unicode(Preferences.getDebugger("RemoteHost"))
            if rhost == "":
                rhost = "localhost"
            if rexec:
                args = Utilities.parseOptionString(rexec) + \
                       [rhost, interpreter, os.path.abspath(debugClient),
                        noencoding, str(port), redirect, ipaddr]
                args[0] = Utilities.getExecutablePath(args[0])
                process = self.__startProcess(args[0], args[1:])
                if process is None:
                    KQMessageBox.critical(None,
                        self.trUtf8("Start Debugger"),
                        self.trUtf8(\
                            """<p>The debugger backend could not be started.</p>"""))
                
                # set translation function
                if Preferences.getDebugger("PathTranslation"):
                    self.translateRemote = \
                        unicode(Preferences.getDebugger("PathTranslationRemote"))
                    self.translateLocal = \
                        unicode(Preferences.getDebugger("PathTranslationLocal"))
                    self.translate = self.__remoteTranslation
                else:
                    self.translate = self.__identityTranslation
                return process, self.__isNetworked
        
        # set translation function
        self.translate = self.__identityTranslation
        
        # setup the environment for the debugger
        if Preferences.getDebugger("DebugEnvironmentReplace"):
            clientEnv = {}
        else:
            clientEnv = os.environ.copy()
        envlist = Utilities.parseEnvironmentString(\
            Preferences.getDebugger("DebugEnvironment"))
        for el in envlist:
            try:
                key, value = el.split('=', 1)
                if value.startswith('"') or value.startswith("'"):
                    value = value[1:-1]
                clientEnv[str(key)] = str(value)
            except UnpackError:
                pass
        
        ipaddr = self.debugServer.getHostAddress(True)
        if runInConsole or Preferences.getDebugger("ConsoleDbgEnabled"):
            ccmd = unicode(Preferences.getDebugger("ConsoleDbgCommand"))
            if ccmd:
                args = Utilities.parseOptionString(ccmd) + \
                       [interpreter, os.path.abspath(debugClient),
                        noencoding, str(port), '0', ipaddr]
                args[0] = Utilities.getExecutablePath(args[0])
                process = self.__startProcess(args[0], args[1:], clientEnv)
                if process is None:
                    KQMessageBox.critical(None,
                        self.trUtf8("Start Debugger"),
                        self.trUtf8(\
                            """<p>The debugger backend could not be started.</p>"""))
                return process, self.__isNetworked
        
        process = self.__startProcess(interpreter, 
            [debugClient, noencoding, str(port), redirect, ipaddr], 
            clientEnv)
        if process is None:
            KQMessageBox.critical(None,
                self.trUtf8("Start Debugger"),
                self.trUtf8("""<p>The debugger backend could not be started.</p>"""))
        return process, self.__isNetworked

    def startRemoteForProject(self, port, runInConsole):
        """
        Public method to start a remote Python interpreter for a project.
        
        @param port portnumber the debug server is listening on (integer)
        @param runInConsole flag indicating to start the debugger in a 
            console window (boolean)
        @return client process object (QProcess) and a flag to indicate
            a network connection (boolean)
        """
        project = e4App().getObject("Project")
        if not project.isDebugPropertiesLoaded():
            return None, self.__isNetworked
        
        # start debugger with project specific settings
        interpreter = project.getDebugProperty("INTERPRETER")
        debugClient = project.getDebugProperty("DEBUGCLIENT")
        
        redirect = str(project.getDebugProperty("REDIRECT"))
        noencoding = \
            project.getDebugProperty("NOENCODING") and '--no-encoding' or ''
        
        if project.getDebugProperty("REMOTEDEBUGGER"):
            ipaddr = self.debugServer.getHostAddress(False)
            rexec = project.getDebugProperty("REMOTECOMMAND")
            rhost = project.getDebugProperty("REMOTEHOST")
            if rhost == "":
                rhost = "localhost"
            if rexec:
                args = Utilities.parseOptionString(rexec) + \
                       [rhost, interpreter, os.path.abspath(debugClient),
                        noencoding, str(port), redirect, ipaddr]
                args[0] = Utilities.getExecutablePath(args[0])
                process = self.__startProcess(args[0], args[1:])
                if process is None:
                    KQMessageBox.critical(None,
                        self.trUtf8("Start Debugger"),
                        self.trUtf8(\
                            """<p>The debugger backend could not be started.</p>"""))
                # set translation function
                if project.getDebugProperty("PATHTRANSLATION"):
                    self.translateRemote = project.getDebugProperty("REMOTEPATH")
                    self.translateLocal = project.getDebugProperty("LOCALPATH")
                    self.translate = self.__remoteTranslation
                else:
                    self.translate = self.__identityTranslation
                return process, self.__isNetworked
        
        # set translation function
        self.translate = self.__identityTranslation
        
        # setup the environment for the debugger
        if project.getDebugProperty("ENVIRONMENTOVERRIDE"):
            clientEnv = {}
        else:
            clientEnv = os.environ.copy()
        envlist = Utilities.parseEnvironmentString(\
            project.getDebugProperty("ENVIRONMENTSTRING"))
        for el in envlist:
            try:
                key, value = el.split('=', 1)
                if value.startswith('"') or value.startswith("'"):
                    value = value[1:-1]
                clientEnv[str(key)] = str(value)
            except UnpackError:
                pass
        
        ipaddr = self.debugServer.getHostAddress(True)
        if runInConsole or project.getDebugProperty("CONSOLEDEBUGGER"):
            ccmd = unicode(project.getDebugProperty("CONSOLECOMMAND")) or \
                   unicode(Preferences.getDebugger("ConsoleDbgCommand"))
            if ccmd:
                args = Utilities.parseOptionString(ccmd) + \
                       [interpreter, os.path.abspath(debugClient),
                        noencoding, str(port), '0', ipaddr]
                args[0] = Utilities.getExecutablePath(args[0])
                process = self.__startProcess(args[0], args[1:], clientEnv)
                if process is None:
                    KQMessageBox.critical(None,
                        self.trUtf8("Start Debugger"),
                        self.trUtf8(\
                            """<p>The debugger backend could not be started.</p>"""))
                return process, self.__isNetworked
        
        process = self.__startProcess(interpreter, 
            [debugClient, noencoding, str(port), redirect, ipaddr], 
            clientEnv)
        if process is None:
            KQMessageBox.critical(None,
                self.trUtf8("Start Debugger"),
                self.trUtf8("""<p>The debugger backend could not be started.</p>"""))
        return process, self.__isNetworked

    def getClientCapabilities(self):
        """
        Public method to retrieve the debug clients capabilities.
        
        @return debug client capabilities (integer)
        """
        return self.clientCapabilities
    
    def newConnection(self, sock):
        """
        Public slot to handle a new connection.
        
        @param sockreference to the socket object (QTcpSocket)
        @return flag indicating success (boolean)
        """
        # If we already have a connection, refuse this one.  It will be closed
        # automatically.
        if self.qsock is not None:
            return False
        
        self.connect(sock, SIGNAL('disconnected()'), self.debugServer.startClient)
        self.connect(sock, SIGNAL('readyRead()'), self.__parseClientLine)
        
        self.qsock = sock
        
        # Get the remote clients capabilities
        self.remoteCapabilities()
        return True
    
    def flush(self):
        """
        Public slot to flush the queue.
        """
        # Send commands that were waiting for the connection.
        for cmd in self.queue:
            self.qsock.write(cmd.encode('utf8', 'backslashreplace'))
        
        self.queue = []
    
    def shutdown(self):
        """
        Public method to cleanly shut down.
        
        It closes our socket and shuts down
        the debug client. (Needed on Win OS)
        """
        if self.qsock is None:
            return
        
        # do not want any slots called during shutdown
        self.disconnect(self.qsock, SIGNAL('disconnected()'), 
            self.debugServer.startClient)
        self.disconnect(self.qsock, SIGNAL('readyRead()'), self.__parseClientLine)
        
        # close down socket, and shut down client as well.
        self.__sendCommand('%s\n' % RequestShutdown)
        self.qsock.flush()
        
        self.qsock.close()
        
        # reinitialize
        self.qsock = None
        self.queue = []
    
    def isConnected(self):
        """
        Public method to test, if a debug client has connected.
        
        @return flag indicating the connection status (boolean)
        """
        return self.qsock is not None
    
    def remoteEnvironment(self, env):
        """
        Public method to set the environment for a program to debug, run, ...
        
        @param env environment settings (dictionary)
        """
        self.__sendCommand('%s%s\n' % (RequestEnv, unicode(env)))
    
    def remoteLoad(self, fn, argv, wd, traceInterpreter = False, autoContinue = True, 
                   autoFork = False, forkChild = False):
        """
        Public method to load a new program to debug.
        
        @param fn the filename to debug (string)
        @param argv the commandline arguments to pass to the program (string or QString)
        @param wd the working directory for the program (string)
        @keyparam traceInterpreter flag indicating if the interpreter library should be
            traced as well (boolean)
        @keyparam autoContinue flag indicating, that the debugger should not stop
            at the first executable line (boolean)
        @keyparam autoFork flag indicating the automatic fork mode (boolean)
        @keyparam forkChild flag indicating to debug the child after forking (boolean)
        """
        self.__autoContinue = autoContinue
        
        wd = self.translate(wd, False)
        fn = self.translate(os.path.abspath(unicode(fn)), False)
        self.__sendCommand('%s%s\n' % (RequestForkMode, repr((autoFork, forkChild))))
        self.__sendCommand('%s%s|%s|%s|%d\n' % \
            (RequestLoad, unicode(wd),
             unicode(fn), unicode(Utilities.parseOptionString(argv)), traceInterpreter))
    
    def remoteRun(self, fn, argv, wd):
        """
        Public method to load a new program to run.
        
        @param fn the filename to run (string)
        @param argv the commandline arguments to pass to the program (string or QString)
        @param wd the working directory for the program (string)
        """
        wd = self.translate(wd, False)
        fn = self.translate(os.path.abspath(unicode(fn)), False)
        self.__sendCommand('%s%s|%s|%s\n' % \
            (RequestRun, unicode(wd),
             unicode(fn), unicode(Utilities.parseOptionString(argv))))
    
    def remoteCoverage(self, fn, argv, wd, erase = False):
        """
        Public method to load a new program to collect coverage data.
        
        @param fn the filename to run (string)
        @param argv the commandline arguments to pass to the program (string or QString)
        @param wd the working directory for the program (string)
        @keyparam erase flag indicating that coverage info should be 
            cleared first (boolean)
        """
        wd = self.translate(wd, False)
        fn = self.translate(os.path.abspath(unicode(fn)), False)
        self.__sendCommand('%s%s@@%s@@%s@@%d\n' % \
            (RequestCoverage, unicode(wd),
             unicode(fn), unicode(Utilities.parseOptionString(argv)),
             erase))

    def remoteProfile(self, fn, argv, wd, erase = False):
        """
        Public method to load a new program to collect profiling data.
        
        @param fn the filename to run (string)
        @param argv the commandline arguments to pass to the program (string or QString)
        @param wd the working directory for the program (string)
        @keyparam erase flag indicating that timing info should be cleared first (boolean)
        """
        wd = self.translate(wd, False)
        fn = self.translate(os.path.abspath(unicode(fn)), False)
        self.__sendCommand('%s%s|%s|%s|%d\n' % \
            (RequestProfile, unicode(wd),
             unicode(fn), unicode(Utilities.parseOptionString(argv)), erase))

    def remoteStatement(self, stmt):
        """
        Public method to execute a Python statement.  
        
        @param stmt the Python statement to execute (string). It
              should not have a trailing newline.
        """
        self.__sendCommand('%s\n' % stmt)
        self.__sendCommand(RequestOK + '\n')

    def remoteStep(self):
        """
        Public method to single step the debugged program.
        """
        self.__sendCommand(RequestStep + '\n')

    def remoteStepOver(self):
        """
        Public method to step over the debugged program.
        """
        self.__sendCommand(RequestStepOver + '\n')

    def remoteStepOut(self):
        """
        Public method to step out the debugged program.
        """
        self.__sendCommand(RequestStepOut + '\n')

    def remoteStepQuit(self):
        """
        Public method to stop the debugged program.
        """
        self.__sendCommand(RequestStepQuit + '\n')

    def remoteContinue(self, special = False):
        """
        Public method to continue the debugged program.
        
        @param special flag indicating a special continue operation
        """
        self.__sendCommand('%s%d\n' % (RequestContinue, special))

    def remoteBreakpoint(self, fn, line, set, cond = None, temp = False):
        """
        Public method to set or clear a breakpoint.
        
        @param fn filename the breakpoint belongs to (string)
        @param line linenumber of the breakpoint (int)
        @param set flag indicating setting or resetting a breakpoint (boolean)
        @param cond condition of the breakpoint (string)
        @param temp flag indicating a temporary breakpoint (boolean)
        """
        fn = self.translate(fn, False)
        self.__sendCommand('%s%s@@%d@@%d@@%d@@%s\n' % \
                           (RequestBreak, fn, line, temp, set, cond))
    
    def remoteBreakpointEnable(self, fn, line, enable):
        """
        Public method to enable or disable a breakpoint.
        
        @param fn filename the breakpoint belongs to (string)
        @param line linenumber of the breakpoint (int)
        @param enable flag indicating enabling or disabling a breakpoint (boolean)
        """
        fn = self.translate(fn, False)
        self.__sendCommand('%s%s,%d,%d\n' % (RequestBreakEnable, fn, line, enable))
    
    def remoteBreakpointIgnore(self, fn, line, count):
        """
        Public method to ignore a breakpoint the next couple of occurrences.
        
        @param fn filename the breakpoint belongs to (string)
        @param line linenumber of the breakpoint (int)
        @param count number of occurrences to ignore (int)
        """
        fn = self.translate(fn, False)
        self.__sendCommand('%s%s,%d,%d\n' % (RequestBreakIgnore, fn, line, count))
    
    def remoteWatchpoint(self, cond, set, temp = False):
        """
        Public method to set or clear a watch expression.
        
        @param cond expression of the watch expression (string)
        @param set flag indicating setting or resetting a watch expression (boolean)
        @param temp flag indicating a temporary watch expression (boolean)
        """
        # cond is combination of cond and special (s. watch expression viewer)
        self.__sendCommand('%s%s@@%d@@%d\n' % (RequestWatch, cond, temp, set))
    
    def remoteWatchpointEnable(self, cond, enable):
        """
        Public method to enable or disable a watch expression.
        
        @param cond expression of the watch expression (string)
        @param enable flag indicating enabling or disabling a watch expression (boolean)
        """
        # cond is combination of cond and special (s. watch expression viewer)
        self.__sendCommand('%s%s,%d\n' % (RequestWatchEnable, cond, enable))
    
    def remoteWatchpointIgnore(self, cond, count):
        """
        Public method to ignore a watch expression the next couple of occurrences.
        
        @param cond expression of the watch expression (string)
        @param count number of occurrences to ignore (int)
        """
        # cond is combination of cond and special (s. watch expression viewer)
        self.__sendCommand('%s%s,%d\n' % (RequestWatchIgnore, cond, count))
    
    def remoteRawInput(self,s):
        """
        Public method to send the raw input to the debugged program.
        
        @param s the raw input (string)
        """
        self.__sendCommand(s + '\n')
    
    def remoteThreadList(self):
        """
        Public method to request the list of threads from the client.
        """
        self.__sendCommand('%s\n' % RequestThreadList)
        
    def remoteSetThread(self, tid):
        """
        Public method to request to set the given thread as current thread.
        
        @param tid id of the thread (integer)
        """
        self.__sendCommand('%s%d\n' % (RequestThreadSet, tid))
        
    def remoteClientVariables(self, scope, filter, framenr = 0):
        """
        Public method to request the variables of the debugged program.
        
        @param scope the scope of the variables (0 = local, 1 = global)
        @param filter list of variable types to filter out (list of int)
        @param framenr framenumber of the variables to retrieve (int)
        """
        self.__sendCommand('%s%d, %d, %s\n' % \
            (RequestVariables, framenr, scope, unicode(filter)))
    
    def remoteClientVariable(self, scope, filter, var, framenr = 0):
        """
        Public method to request the variables of the debugged program.
        
        @param scope the scope of the variables (0 = local, 1 = global)
        @param filter list of variable types to filter out (list of int)
        @param var list encoded name of variable to retrieve (string)
        @param framenr framenumber of the variables to retrieve (int)
        """
        self.__sendCommand('%s%s, %d, %d, %s\n' % \
            (RequestVariable, str(var), framenr, scope, str(filter)))
    
    def remoteClientSetFilter(self, scope, filter):
        """
        Public method to set a variables filter list.
        
        @param scope the scope of the variables (0 = local, 1 = global)
        @param filter regexp string for variable names to filter out (string)
        """
        self.__sendCommand('%s%d, "%s"\n' % (RequestSetFilter, scope, filter))
    
    def remoteEval(self, arg):
        """
        Public method to evaluate arg in the current context of the debugged program.
        
        @param arg the arguments to evaluate (string)
        """
        self.__sendCommand('%s%s\n' % (RequestEval, arg))
    
    def remoteExec(self, stmt):
        """
        Public method to execute stmt in the current context of the debugged program.
        
        @param stmt statement to execute (string)
        """
        self.__sendCommand('%s%s\n' % (RequestExec, stmt))
    
    def remoteBanner(self):
        """
        Public slot to get the banner info of the remote client.
        """
        self.__sendCommand(RequestBanner + '\n')
    
    def remoteCapabilities(self):
        """
        Public slot to get the debug clients capabilities.
        """
        self.__sendCommand(RequestCapabilities + '\n')
    
    def remoteCompletion(self, text):
        """
        Public slot to get the a list of possible commandline completions
        from the remote client.
        
        @param text the text to be completed (string or QString)
        """
        self.__sendCommand("%s%s\n" % (RequestCompletion, text))
    
    def remoteUTPrepare(self, fn, tn, tfn, cov, covname, coverase):
        """
        Public method to prepare a new unittest run.
        
        @param fn the filename to load (string)
        @param tn the testname to load (string)
        @param tfn the test function name to load tests from (string)
        @param cov flag indicating collection of coverage data is requested
        @param covname filename to be used to assemble the coverage caches
                filename
        @param coverase flag indicating erasure of coverage data is requested
        """
        fn = self.translate(os.path.abspath(unicode(fn)), False)
        self.__sendCommand('%s%s|%s|%s|%d|%s|%d\n' % \
            (RequestUTPrepare,
             unicode(fn), unicode(tn), unicode(tfn), cov, unicode(covname), coverase))
    
    def remoteUTRun(self):
        """
        Public method to start a unittest run.
        """
        self.__sendCommand('%s\n' % RequestUTRun)
    
    def remoteUTStop(self):
        """
        Public method to stop a unittest run.
        """
        self.__sendCommand('%s\n' % RequestUTStop)
    
    def __askForkTo(self):
        """
        Private method to ask the user which branch of a fork to follow.
        """
        selections = [self.trUtf8("Parent Process"), self.trUtf8("Child process")]
        res, ok = KQInputDialog.getItem(\
            None,
            self.trUtf8("Client forking"),
            self.trUtf8("Select the fork branch to follow."),
            selections,
            0, False)
        if not ok or res == selections[0]:
            self.__sendCommand(ResponseForkTo + 'parent\n')
        else:
            self.__sendCommand(ResponseForkTo + 'child\n')
    
    def __parseClientLine(self):
        """
        Private method to handle data from the client.
        """
        while self.qsock and self.qsock.canReadLine():
            qs = self.qsock.readLine()
            if self.codec is not None:
                us = self.codec.fromUnicode(QString(qs))
            else:
                us = qs
            line = str(us)
            if line.endswith(EOT):
                line = line[:-len(EOT)]
                if not line:
                    continue
            
##            print "Server: ", line          ##debug
            
            eoc = line.find('<') + 1
            
            # Deal with case where user has written directly to stdout
            # or stderr, but not line terminated and we stepped over the
            # write call, in that case the >line< will not be the first
            # string read from the socket...
            boc = line.find('>')
            if boc > 0 and eoc > boc:
                self.debugServer.clientOutput(line[:boc])
                line = line[boc:]
                eoc = line.find('<') + 1
                boc = line.find('>')
            
            if boc >= 0 and eoc > boc:
                resp = line[boc:eoc]
                
                if resp == ResponseLine or resp == ResponseStack:
                    stack = eval(line[eoc:-1])
                    for s in stack:
                        s[0] = self.translate(s[0], True)
                    cf = stack[0]
                    if self.__autoContinue:
                        self.__autoContinue = False
                        QTimer.singleShot(0, self.remoteContinue)
                    else:
                        self.debugServer.clientLine(cf[0], int(cf[1]), 
                                                    resp == ResponseStack)
                        self.debugServer.clientStack(stack)
                    continue
                
                if resp == ResponseThreadList:
                    currentId, threadList = eval(line[eoc:-1])
                    self.debugServer.clientThreadList(currentId, threadList)
                    continue
                
                if resp == ResponseThreadSet:
                    self.debugServer.clientThreadSet()
                    continue
                
                if resp == ResponseVariables:
                    vlist = eval(line[eoc:-1])
                    scope = vlist[0]
                    try:
                        variables = vlist[1:]
                    except IndexError:
                        variables = []
                    self.debugServer.clientVariables(scope, variables)
                    continue
                
                if resp == ResponseVariable:
                    vlist = eval(line[eoc:-1])
                    scope = vlist[0]
                    try:
                        variables = vlist[1:]
                    except IndexError:
                        variables = []
                    self.debugServer.clientVariable(scope, variables)
                    continue
                
                if resp == ResponseOK:
                    self.debugServer.clientStatement(False)
                    continue
                
                if resp == ResponseContinue:
                    self.debugServer.clientStatement(True)
                    continue
                
                if resp == ResponseException:
                    exc = line[eoc:-1]
                    exc = self.translate(exc, True)
                    try:
                        exclist = eval(exc)
                        exctype = exclist[0]
                        excmessage = exclist[1]
                        stack = exclist[2:]
                    except (IndexError, ValueError, SyntaxError):
                        exctype = None
                        excmessage = ''
                        stack = []
                    self.debugServer.clientException(exctype, excmessage, stack)
                    continue
                
                if resp == ResponseSyntax:
                    exc = line[eoc:-1]
                    exc = self.translate(exc, True)
                    try:
                        message, (fn, ln, cn) = eval(exc)
                        if fn is None:
                            fn = ''
                    except (IndexError, ValueError):
                        message = None
                        fn = ''
                        ln = 0
                        cn = 0
                    if cn is None:
                        cn = 0
                    self.debugServer.clientSyntaxError(message, fn, ln, cn)
                    continue
                
                if resp == ResponseExit:
                    self.debugServer.clientExit(line[eoc:-1])
                    continue
                
                if resp == ResponseClearBreak:
                    fn, lineno = line[eoc:-1].split(',')
                    lineno = int(lineno)
                    fn = self.translate(fn, True)
                    self.debugServer.clientClearBreak(fn, lineno)
                    continue
                
                if resp == ResponseBPConditionError:
                    fn, lineno = line[eoc:-1].split(',')
                    lineno = int(lineno)
                    fn = self.translate(fn, True)
                    self.debugServer.clientBreakConditionError(fn, lineno)
                    continue
                
                if resp == ResponseClearWatch:
                    cond = line[eoc:-1]
                    self.debugServer.clientClearWatch(cond)
                    continue
                
                if resp == ResponseWPConditionError:
                    cond = line[eoc:-1]
                    self.debugServer.clientWatchConditionError(cond)
                    continue
                
                if resp == ResponseRaw:
                    prompt, echo = eval(line[eoc:-1])
                    self.debugServer.clientRawInput(prompt, echo)
                    continue
                
                if resp == ResponseBanner:
                    version, platform, dbgclient = eval(line[eoc:-1])
                    self.debugServer.clientBanner(version, platform, dbgclient)
                    continue
                
                if resp == ResponseCapabilities:
                    cap, clType = eval(line[eoc:-1])
                    self.clientCapabilities = cap
                    self.debugServer.clientCapabilities(cap, clType)
                    continue
                
                if resp == ResponseCompletion:
                    clstring, text = line[eoc:-1].split('||')
                    cl = eval(clstring)
                    self.debugServer.clientCompletionList(cl, text)
                    continue
                
                if resp == PassiveStartup:
                    fn, exc = line[eoc:-1].split('|')
                    exc = bool(exc)
                    fn = self.translate(fn, True)
                    self.debugServer.passiveStartUp(fn, exc)
                    continue
                
                if resp == ResponseUTPrepared:
                    res, exc_type, exc_value = eval(line[eoc:-1])
                    self.debugServer.clientUtPrepared(res, exc_type, exc_value)
                    continue
                
                if resp == ResponseUTStartTest:
                    testname, doc = eval(line[eoc:-1])
                    self.debugServer.clientUtStartTest(testname, doc)
                    continue
                
                if resp == ResponseUTStopTest:
                    self.debugServer.clientUtStopTest()
                    continue
                
                if resp == ResponseUTTestFailed:
                    testname, traceback = eval(line[eoc:-1])
                    self.debugServer.clientUtTestFailed(testname, traceback)
                    continue
                
                if resp == ResponseUTTestErrored:
                    testname, traceback = eval(line[eoc:-1])
                    self.debugServer.clientUtTestErrored(testname, traceback)
                    continue
                
                if resp == ResponseUTFinished:
                    self.debugServer.clientUtFinished()
                    continue
                
                if resp == RequestForkTo:
                    self.__askForkTo()
                    continue
            
            self.debugServer.clientOutput(line)

    def __sendCommand(self, cmd):
        """
        Private method to send a single line command to the client.
        
        @param cmd command to send to the debug client (string)
        """
        if self.qsock is not None:
            self.qsock.write(cmd.encode('utf8', 'backslashreplace'))
        else:
            self.queue.append(cmd)
