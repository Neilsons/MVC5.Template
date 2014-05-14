﻿using System;
using System.Collections.Generic;
using System.Linq;
using Template.Data.Core;
using Template.Objects;
using Template.Resources;
using Template.Resources.Views.RoleView;

namespace Template.Services
{
    public class RolesService : GenericService<Role, RoleView>, IRolesService
    {
        public RolesService(IUnitOfWork unitOfWork)
            : base(unitOfWork)
        {
        }

        public override Boolean CanCreate(RoleView view)
        {
            Boolean isValid = base.CanCreate(view);
            isValid &= IsUniqueRole(view);

            return isValid;
        }
        public override Boolean CanEdit(RoleView view)
        {
            Boolean isValid = base.CanEdit(view);
            isValid &= IsUniqueRole(view);

            return isValid;
        }

        public override RoleView GetView(String id)
        {
            RoleView role = base.GetView(id);
            SeedPrivilegesTree(role);

            return role;
        }

        public override void Create(RoleView view)
        {
            // TODO: Add validation for same role names
            CreateRole(view);
            CreateRolePrivileges(view);
            UnitOfWork.Commit();
        }
        public override void Edit(RoleView view)
        {
            EditRole(view);
            DeleteRolePrivileges(view);
            CreateRolePrivileges(view);
            UnitOfWork.Commit();
        }
        public override void Delete(String id)
        {
            RemoveRoleFromPeople(id);
            UnitOfWork.Repository<Role>().Delete(id);
            UnitOfWork.Commit();
        }

        public virtual void SeedPrivilegesTree(RoleView view)
        {
            TreeNode rootNode = new TreeNode();
            view.PrivilegesTree = new Tree();
            view.PrivilegesTree.Nodes.Add(rootNode);
            rootNode.Name = Resources.Privilege.Titles.All;
            view.PrivilegesTree.SelectedIds = UnitOfWork
                .Repository<RolePrivilege>()
                .Query(rolePrivilege => rolePrivilege.RoleId == view.Id)
                .Select(rolePrivilege => rolePrivilege.PrivilegeId).ToList();
            
            IEnumerable<IGrouping<String, Privilege>> allPrivileges = UnitOfWork
                .Repository<Privilege>()
                .Query()
                .ToList()
                .Select(privilege => new Privilege
                    {
                        Id = privilege.Id,
                        Area = ResourceProvider.GetPrivilegeAreaTitle(privilege.Area),
                        Action = ResourceProvider.GetPrivilegeActionTitle(privilege.Action),
                        Controller = ResourceProvider.GetPrivilegeControllerTitle(privilege.Controller)
                    })
                .GroupBy(privilege => privilege.Area)
                .OrderBy(privilege => privilege.Key ?? privilege.FirstOrDefault().Controller);

            foreach (IGrouping<String, Privilege> areaPrivilege in allPrivileges)
            {
                TreeNode areaNode = new TreeNode(areaPrivilege.Key);
                foreach (IGrouping<String, Privilege> controllerPrivilege in areaPrivilege.GroupBy(privilege => privilege.Controller).OrderBy(privilege => privilege.Key))
                {
                    TreeNode controllerNode = new TreeNode(controllerPrivilege.Key);
                    foreach (IGrouping<String, Privilege> actionPrivilege in controllerPrivilege.GroupBy(privilege => privilege.Action).OrderBy(privilege => privilege.Key))
                        controllerNode.Nodes.Add(new TreeNode(actionPrivilege.First().Id, actionPrivilege.Key));

                    if (areaNode.Name == null)
                        rootNode.Nodes.Add(controllerNode);
                    else
                        areaNode.Nodes.Add(controllerNode);
                }

                if (areaNode.Name != null)
                    rootNode.Nodes.Add(areaNode);
            }
        }

        private Boolean IsUniqueRole(RoleView view)
        {
            Boolean isUnique = !UnitOfWork.Repository<Role>()
                .Query(role =>
                    role.Id != view.Id &&
                    role.Name.ToUpper() == view.Name.ToUpper())
                .Any();

            if (!isUnique)
                ModelState.AddModelError("Name", Validations.RoleNameIsAlreadyTaken);

            return isUnique;
        }

        private void CreateRole(RoleView view)
        {
            Role model = UnitOfWork.ToModel<RoleView, Role>(view);
            UnitOfWork.Repository<Role>().Insert(model);
        }
        private void EditRole(RoleView view)
        {
            Role model = UnitOfWork.ToModel<RoleView, Role>(view);
            UnitOfWork.Repository<Role>().Update(model);
        }

        private void DeleteRolePrivileges(RoleView view)
        {
            IQueryable<String> rolePrivileges = UnitOfWork.Repository<RolePrivilege>()
                .Query(rolePrivilege => rolePrivilege.RoleId == view.Id)
                .Select(rolePrivilege => rolePrivilege.Id);

            foreach (String rolePrivilege in rolePrivileges)
                UnitOfWork.Repository<RolePrivilege>().Delete(rolePrivilege);
        }
        private void CreateRolePrivileges(RoleView view)
        {
            foreach (String privilegeId in view.PrivilegesTree.SelectedIds)
                UnitOfWork.Repository<RolePrivilege>().Insert(new RolePrivilege()
                {
                    RoleId = view.Id,
                    PrivilegeId = privilegeId
                });
        }

        private void RemoveRoleFromPeople(String roleId)
        {
            IQueryable<Person> peopleWithRole = UnitOfWork.Repository<Person>()
                .Query(person => person.RoleId == roleId);

            foreach (Person person in peopleWithRole)
            {
                person.RoleId = null;
                UnitOfWork.Repository<Person>().Update(person);
            }
        }
    }
}
