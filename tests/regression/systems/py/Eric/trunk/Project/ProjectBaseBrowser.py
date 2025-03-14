# -*- coding: utf-8 -*-

# Copyright (c) 2002 - 2010 Detlev Offenbach <detlev@die-offenbachs.de>
#

"""
Module implementing the baseclass for the various project browsers.
"""

import os
import sys

from PyQt4.QtCore import *
from PyQt4.QtGui import *

from KdeQt import KQMessageBox
from KdeQt.KQApplication import e4App

from UI.Browser import *

from ProjectBrowserModel import *
from ProjectBrowserSortFilterProxyModel import ProjectBrowserSortFilterProxyModel

import UI.PixmapCache
import Preferences

class ProjectBaseBrowser(Browser):
    """
    Baseclass implementing common functionality for the various project browsers.
    """
    def __init__(self, project, type_, parent = None):
        """
        Constructor
        
        @param project reference to the project object
        @param type project browser type (string)
        @param parent parent widget of this browser
        """
        QTreeView.__init__(self, parent)
        
        self.project = project
        
        self._model = project.getModel()
        self._sortModel = ProjectBrowserSortFilterProxyModel(type_)
        self._sortModel.setSourceModel(self._model)
        self.setModel(self._sortModel)
        
        self.selectedItemsFilter = [ProjectBrowserFileItem]
        
        # contains codes for special menu entries
        # 1 = specials for Others browser
        self.specialMenuEntries = []
        self.isTranslationsBrowser = False
        self.expandedNames = []
        
        self.SelectFlags = QItemSelectionModel.SelectionFlags(\
            QItemSelectionModel.Select | QItemSelectionModel.Rows)
        self.DeselectFlags = QItemSelectionModel.SelectionFlags(\
            QItemSelectionModel.Deselect | QItemSelectionModel.Rows)
        
        self.setContextMenuPolicy(Qt.CustomContextMenu)
        self.connect(self, SIGNAL("customContextMenuRequested(const QPoint &)"),
                     self._contextMenuRequested)
        self.connect(self, SIGNAL("activated(const QModelIndex &)"), self._openItem)
        self.connect(self._model, SIGNAL("rowsInserted(const QModelIndex &, int, int)"),
                     self.__modelRowsInserted)
        self._connectExpandedCollapsed()
        
        self._createPopupMenus()
        
        self.currentItemName = None
        
        self._init()    # perform common initialization tasks
        
        self._initHookMethods()     # perform initialization of the hooks
        self.hooksMenuEntries = {}
        
    def _connectExpandedCollapsed(self):
        """
        Protected method to connect the expanded and collapsed signals.
        """
        self.connect(self, SIGNAL("expanded(const QModelIndex &)"), 
            self._resizeColumns)
        self.connect(self, SIGNAL("collapsed(const QModelIndex &)"), 
            self._resizeColumns)
        
    def _disconnectExpandedCollapsed(self):
        """
        Protected method to disconnect the expanded and collapsed signals.
        """
        self.disconnect(self, SIGNAL("expanded(const QModelIndex &)"), 
            self._resizeColumns)
        self.disconnect(self, SIGNAL("collapsed(const QModelIndex &)"), 
            self._resizeColumns)
        
    def _createPopupMenus(self):
        """
        Protected overloaded method to generate the popup menus.
        """
        # create the popup menu for source files
        self.sourceMenu = QMenu(self)
        self.sourceMenu.addAction(
            QApplication.translate('ProjectBaseBrowser', 'Open'), 
            self._openItem)
        
        # create the popup menu for general use
        self.menu = QMenu(self)
        self.menu.addAction(
            QApplication.translate('ProjectBaseBrowser', 'Open'), 
            self._openItem)

        # create the menu for multiple selected files
        self.multiMenu = QMenu(self)
        self.multiMenu.addAction(QApplication.translate('ProjectBaseBrowser', 'Open'), 
            self._openItem)
        
        # create the background menu
        self.backMenu = None
        
        # create the directories menu
        self.dirMenu = None
        
        # create the directory for multiple selected directories
        self.dirMultiMenu = None
        
        self.menuActions = []
        self.multiMenuActions = []
        self.dirMenuActions = []
        self.dirMultiMenuActions = []
        
        self.mainMenu = None
        
    def _contextMenuRequested(self, coord):
        """
        Protected slot to show the context menu.
        
        @param coord the position of the mouse pointer (QPoint)
        """
        if not self.project.isOpen():
            return
        
        cnt = self.getSelectedItemsCount()
        if cnt > 1:
            self.multiMenu.popup(self.mapToGlobal(coord))
        else:
            index = self.indexAt(coord)
            
            if index.isValid():
                self.menu.popup(self.mapToGlobal(coord))
            else:
                self.backMenu and self.backMenu.popup(self.mapToGlobal(coord))
        
    def _selectSingleItem(self, index):
        """
        Protected method to select a single item.
        
        @param index index of item to be selected (QModelIndex)
        """
        if index.isValid():
            self.setCurrentIndex(index)
            flags = QItemSelectionModel.SelectionFlags(\
                QItemSelectionModel.ClearAndSelect | QItemSelectionModel.Rows)
            self.selectionModel().select(index, flags)
        
    def _setItemSelected(self, index, selected):
        """
        Protected method to set the selection status of an item.
        
        @param index index of item to set (QModelIndex)
        @param selected flag giving the new selection status (boolean)
        """
        if index.isValid():
            self.selectionModel().select(index, 
                selected and self.SelectFlags or self.DeselectFlags)
        
    def _setItemRangeSelected(self, startIndex, endIndex, selected):
        """
        Protected method to set the selection status of a range of items.
        
        @param startIndex start index of range of items to set (QModelIndex)
        @param endIndex end index of range of items to set (QModelIndex)
        @param selected flag giving the new selection status (boolean)
        """
        selection = QItemSelection(startIndex, endIndex)
        self.selectionModel().select(selection, 
            selected and self.SelectFlags or self.DeselectFlags)
        
    def __modelRowsInserted(self, parent, start, end):
        """
        Private slot called after rows have been inserted into the model.
        
        @param parent parent index of inserted rows (QModelIndex)
        @param start start row number (integer)
        @param end end row number (integer)
        """
        self._resizeColumns(parent)
        
    def __modelDataChanged(self, startIndex, endIndex):
        """
        Private slot called after data has been changed in the model.
        
        @param startIndex start index of the changed data (QModelIndex)
        @param endIndex end index of the changed data (QModelIndex)
        """
        self._resizeColumns(startIndex)
        
    def _projectClosed(self):
        """
        Protected slot to handle the projectClosed signal.
        """
        self.layoutDisplay()
        if self.backMenu is not None:
            self.backMenu.setEnabled(False)
        
        self._createPopupMenus()
        
    def _projectOpened(self):
        """
        Protected slot to handle the projectOpened signal.
        """
        self.layoutDisplay()
        self.sortByColumn(0, Qt.DescendingOrder)
        self.sortByColumn(0, Qt.AscendingOrder)
        self._initMenusAndVcs()
        
    def _initMenusAndVcs(self):
        """
        Protected slot to initialize the menus and the Vcs interface.
        """
        self._createPopupMenus()
        
        if self.backMenu is not None:
            self.backMenu.setEnabled(True)
        
        if self.project.vcs is not None:
            self.vcsHelper = self.project.vcs.vcsGetProjectBrowserHelper(self, 
                self.project, self.isTranslationsBrowser)
            self.vcsHelper.addVCSMenus(self.mainMenu, self.multiMenu,
                self.backMenu, self.dirMenu, self.dirMultiMenu)
        
    def _newProject(self):
        """
        Protected slot to handle the newProject signal.
        """
        self.layoutDisplay()
        self.sortByColumn(0, Qt.DescendingOrder)
        self.sortByColumn(0, Qt.AscendingOrder)
        
        self._createPopupMenus()
        
        if self.backMenu is not None:
            self.backMenu.setEnabled(True)
        
        if self.project.vcs is not None:
            self.vcsHelper = self.project.vcs.vcsGetProjectBrowserHelper(self, 
                self.project, self.isTranslationsBrowser)
            self.vcsHelper.addVCSMenus(self.mainMenu, self.multiMenu,
                self.backMenu, self.dirMenu, self.dirMultiMenu)
        
    def _removeFile(self):
        """
        Protected method to remove a file or files from the project.
        """
        itmList = self.getSelectedItems()
        
        for itm in itmList[:]:
            fn = unicode(itm.fileName())
            self.emit(SIGNAL('closeSourceWindow'), fn)
            self.project.removeFile(fn)
        
    def _removeDir(self):
        """
        Protected method to remove a (single) directory from the project.
        """
        itmList = self.getSelectedItems(\
            [ProjectBrowserSimpleDirectoryItem, ProjectBrowserDirectoryItem])
        for itm in itmList[:]:
            dn = unicode(itm.dirName())
            self.project.removeDirectory(dn)
        
    def _renameFile(self):
        """
        Protected method to rename a file of the project.
        """
        itm = self.model().item(self.currentIndex())
        fn = unicode(itm.fileName())
        self.project.renameFile(fn)
        
    def _copyToClipboard(self):
        """
        Protected method to copy the path of an entry to the clipboard.
        """
        itm = self.model().item(self.currentIndex())
        try:
            fn = unicode(itm.fileName())
        except AttributeError:
            try:
                fn = unicode(itm.dirName())
            except AttributeError:
                fn = ""
        
        cb = QApplication.clipboard()
        cb.setText(fn)
        
    def selectFile(self, fn):
        """
        Public method to highlight a node given its filename.
        
        @param fn filename of file to be highlighted (string or QString)
        """
        newfn = os.path.abspath(unicode(fn))
        newfn = newfn.replace(self.project.ppath + os.sep, '')
        sindex = self._model.itemIndexByName(newfn)
        if sindex.isValid():
            index = self.model().mapFromSource(sindex)
            if index.isValid():
                self._selectSingleItem(index)
                self.scrollTo(index, QAbstractItemView.PositionAtTop)
        
    def _expandAllDirs(self):
        """
        Protected slot to handle the 'Expand all directories' menu action.
        """
        self._disconnectExpandedCollapsed()
        QApplication.setOverrideCursor(QCursor(Qt.WaitCursor))
        QApplication.processEvents()
        index = self.model().index(0, 0)
        while index.isValid():
            itm = self.model().item(index)
            if (isinstance(itm, ProjectBrowserSimpleDirectoryItem) or \
                isinstance(itm, ProjectBrowserDirectoryItem)) and \
               not self.isExpanded(index):
                self.expand(index)
            index = self.indexBelow(index)
        self.layoutDisplay()
        self._connectExpandedCollapsed()
        QApplication.restoreOverrideCursor()
        
    def _collapseAllDirs(self):
        """
        Protected slot to handle the 'Collapse all directories' menu action.
        """
        self._disconnectExpandedCollapsed()
        QApplication.setOverrideCursor(QCursor(Qt.WaitCursor))
        QApplication.processEvents()
        # step 1: find last valid index
        vindex = QModelIndex()
        index = self.model().index(0, 0)
        while index.isValid():
            vindex = index
            index = self.indexBelow(index)
        
        # step 2: go up collapsing all directory items
        index = vindex
        while index.isValid():
            itm = self.model().item(index)
            if (isinstance(itm, ProjectBrowserSimpleDirectoryItem) or \
                isinstance(itm, ProjectBrowserDirectoryItem)) and \
               self.isExpanded(index):
                self.collapse(index)
            index = self.indexAbove(index)
        self.layoutDisplay()
        self._connectExpandedCollapsed()
        QApplication.restoreOverrideCursor()
        
    def _showContextMenu(self, menu):
        """
        Protected slot called before the context menu is shown. 
        
        It enables/disables the VCS menu entries depending on the overall 
        VCS status and the file status.
        
        @param menu reference to the menu to be shown
        """
        if self.project.vcs is None:
            for act in self.menuActions:
                act.setEnabled(True)
        else:
            self.vcsHelper.showContextMenu(menu, self.menuActions)
        
    def _showContextMenuMulti(self, menu):
        """
        Protected slot called before the context menu (multiple selections) is shown. 
        
        It enables/disables the VCS menu entries depending on the overall 
        VCS status and the files status.
        
        @param menu reference to the menu to be shown
        """
        if self.project.vcs is None:
            for act in self.multiMenuActions:
                act.setEnabled(True)
        else:
            self.vcsHelper.showContextMenuMulti(menu, self.multiMenuActions)
        
    def _showContextMenuDir(self, menu):
        """
        Protected slot called before the context menu is shown. 
        
        It enables/disables the VCS menu entries depending on the overall 
        VCS status and the directory status.
        
        @param menu reference to the menu to be shown
        """
        if self.project.vcs is None:
            for act in self.dirMenuActions:
                act.setEnabled(True)
        else:
            self.vcsHelper.showContextMenuDir(menu, self.dirMenuActions)
        
    def _showContextMenuDirMulti(self, menu):
        """
        Protected slot called before the context menu is shown. 
        
        It enables/disables the VCS menu entries depending on the overall 
        VCS status and the directory status.
        
        @param menu reference to the menu to be shown
        """
        if self.project.vcs is None:
            for act in self.dirMultiMenuActions:
                act.setEnabled(True)
        else:
            self.vcsHelper.showContextMenuDirMulti(menu, self.dirMultiMenuActions)
        
    def _showContextMenuBack(self, menu):
        """
        Protected slot called before the context menu is shown. 
        
        @param menu reference to the menu to be shown
        """
        # nothing to do for now
        return
        
    def _selectEntries(self, local = True, filter = None):
        """
        Protected method to select entries based on their VCS status.
        
        @param local flag indicating local (i.e. non VCS controlled) file/directory
            entries should be selected (boolean)
        @param filter list of classes to check against
        """
        if self.project.vcs is None:
            return
        
        if local:
            compareString = QApplication.translate('ProjectBaseBrowser', "local")
        else:
            compareString = QString(self.project.vcs.vcsName())
        
        # expand all directories in order to iterate over all entries
        self._expandAllDirs()
        
        QApplication.setOverrideCursor(QCursor(Qt.WaitCursor))
        QApplication.processEvents()
        self.selectionModel().clear()
        QApplication.processEvents()
        
        # now iterate over all entries
        startIndex = None
        endIndex = None
        selectedEntries = 0
        index = self.model().index(0, 0)
        while index.isValid():
            itm = self.model().item(index)
            if self.wantedItem(itm, filter) and \
               QString.compare(compareString, itm.data(1)) == 0:
                if startIndex is not None and \
                   startIndex.parent() != index.parent():
                    self._setItemRangeSelected(startIndex, endIndex, True)
                    startIndex = None
                selectedEntries += 1
                if startIndex is None:
                    startIndex = index
                endIndex = index
            else:
                if startIndex is not None:
                    self._setItemRangeSelected(startIndex, endIndex, True)
                    startIndex = None
            index = self.indexBelow(index)
        if startIndex is not None:
            self._setItemRangeSelected(startIndex, endIndex, True)
        QApplication.restoreOverrideCursor()
        QApplication.processEvents()
        
        if selectedEntries == 0:
            KQMessageBox.information(None,
            QApplication.translate('ProjectBaseBrowser', "Select entries"),
            QApplication.translate('ProjectBaseBrowser', 
                """There were no matching entries found."""))
        
    def selectLocalEntries(self):
        """
        Public slot to handle the select local files context menu entries
        """
        self._selectEntries(local = True, filter = [ProjectBrowserFileItem])
        
    def selectVCSEntries(self):
        """
        Public slot to handle the select VCS files context menu entries
        """
        self._selectEntries(local = False, filter = [ProjectBrowserFileItem])
        
    def selectLocalDirEntries(self):
        """
        Public slot to handle the select local directories context menu entries
        """
        self._selectEntries(local = True,
            filter=[ProjectBrowserSimpleDirectoryItem, ProjectBrowserDirectoryItem])
        
    def selectVCSDirEntries(self):
        """
        Public slot to handle the select VCS directories context menu entries
        """
        self._selectEntries(local = False,
            filter=[ProjectBrowserSimpleDirectoryItem, ProjectBrowserDirectoryItem])
        
    def _prepareRepopulateItem(self, name):
        """
        Protected slot to handle the prepareRepopulateItem signal.
        
        @param name relative name of file item to be repopulated
        """
        QApplication.setOverrideCursor(QCursor(Qt.WaitCursor))
        QApplication.processEvents()
        itm = self.currentItem()
        if itm is not None:
            self.currentItemName = QString(itm.data(0))
        self.expandedNames = []
        sindex = self._model.itemIndexByName(name)
        if not sindex.isValid():
            return
        
        index = self.model().mapFromSource(sindex)
        if not index.isValid():
            return
        
        childIndex = self.indexBelow(index)
        while childIndex.isValid():
            if childIndex.parent() == index.parent():
                break
            if self.isExpanded(childIndex):
                self.expandedNames.append(QString(self.model().item(childIndex).data(0)))
            childIndex = self.indexBelow(childIndex)
        
    def _completeRepopulateItem(self, name):
        """
        Protected slot to handle the completeRepopulateItem signal.
        
        @param name relative name of file item to be repopulated
        """
        sindex = self._model.itemIndexByName(name)
        if sindex.isValid():
            index = self.model().mapFromSource(sindex)
            if index.isValid():
                childIndex = self.indexBelow(index)
                while childIndex.isValid():
                    if not childIndex.isValid() or childIndex.parent() == index.parent():
                        break
                    itm = self.model().item(childIndex)
                    if itm is not None:
                        itemData = QString(itm.data(0))
                        if self.currentItemName and self.currentItemName == itemData:
                            self._selectSingleItem(childIndex)
                        if itemData in self.expandedNames:
                            self.setExpanded(childIndex, True)
                    childIndex = self.indexBelow(childIndex)
                self.expandedNames = []
        self.currentItemName = None
        QApplication.restoreOverrideCursor()
        QApplication.processEvents()
        self._resort()
        
    def currentItem(self):
        """
        Public method to get a reference to the current item.
        
        @return reference to the current item
        """
        itm = self.model().item(self.currentIndex())
        return itm
    
    ############################################################################
    ## Support for hooks below
    ############################################################################
    
    def _initHookMethods(self):
        """
        Protected method to initialize the hooks dictionary.
        
        This method should be overridden by subclasses. All supported
        hook methods should be initialized with a None value. The keys
        must be strings.
        """
        self.hooks = {}
        
    def __checkHookKey(self, key):
        """
        Private method to check a hook key
        """
        if len(self.hooks) == 0:
            raise KeyError("Hooks are not initialized.")
        
        if key not in self.hooks:
            raise KeyError(key)
        
    def addHookMethod(self, key, method):
        """
        Public method to add a hook method to the dictionary.
        
        @param key for the hook method (string)
        @param method reference to the hook method (method object)
        """
        self.__checkHookKey(key)
        self.hooks[key] = method
        
    def addHookMethodAndMenuEntry(self, key, method, menuEntry):
        """
        Public method to add a hook method to the dictionary.
        
        @param key for the hook method (string)
        @param method reference to the hook method (method object)
        @param menuEntry entry to be shown in the context menu (QString)
        """
        self.addHookMethod(key, method)
        self.hooksMenuEntries[key] = QString(menuEntry)
        
    def removeHookMethod(self, key):
        """
        Public method to remove a hook method from the dictionary.
        
        @param key for the hook method (string)
        """
        self.__checkHookKey(key)
        self.hooks[key] = None
        if key in self.hooksMenuEntries:
            del self.hooksMenuEntries[key]
    
    ##################################################################
    ## Configure method below
    ##################################################################
    
    def _configure(self):
        """
        Protected method to open the configuration dialog.
        """
        e4App().getObject("UserInterface").showPreferences("projectBrowserPage")
